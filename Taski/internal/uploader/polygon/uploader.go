package polygon

import (
	"archive/zip"
	"bytes"
	"context"
	"crypto/sha1"
	"encoding/hex"
	"encoding/json"
	"encoding/xml"
	"errors"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"strconv"
	"strings"
	"taski/internal/domain/task"
	"taski/internal/domain/task/tasks"
	"taski/internal/uploader"
	"time"

	"github.com/DIvanCode/filestorage/pkg/bucket"
	"log/slog"
)

type polygonProblem struct {
	XMLName xml.Name `xml:"problem"`
	Short   string   `xml:"short-name,attr"`

	Names struct {
		Names []struct {
			Language string `xml:"language,attr"`
			Value    string `xml:"value,attr"`
		} `xml:"name"`
	} `xml:"names"`

	Statements struct {
		Statements []struct {
			Language string `xml:"language,attr"`
			Path     string `xml:"path,attr"`
			Type     string `xml:"type,attr"`
		} `xml:"statement"`
	} `xml:"statements"`

	Judging struct {
		Testsets []polygonTestset `xml:"testset"`
	} `xml:"judging"`

	Assets struct {
		Checker struct {
			Source polygonSource `xml:"source"`
		} `xml:"checker"`
		Solutions struct {
			Solutions []struct {
				Tag    string        `xml:"tag,attr"`
				Source polygonSource `xml:"source"`
			} `xml:"solution"`
		} `xml:"solutions"`
	} `xml:"assets"`
}

type polygonSource struct {
	Path string `xml:"path,attr"`
	Type string `xml:"type,attr"`
}

type polygonTestset struct {
	Name              string `xml:"name,attr"`
	TimeLimit         int    `xml:"time-limit"`
	MemoryLimit       int    `xml:"memory-limit"`
	TestCount         int    `xml:"test-count"`
	InputPathPattern  string `xml:"input-path-pattern"`
	AnswerPathPattern string `xml:"answer-path-pattern"`
	Tests             struct {
		Tests []struct {
			Sample string `xml:"sample,attr"`
		} `xml:"test"`
	} `xml:"tests"`
}

type fileStorage interface {
	ReserveBucket(ctx context.Context, id bucket.ID, ttl *time.Duration) (path string, commit, abort func() error, err error)
}

type polygonUploader struct {
	fs  fileStorage
	log *slog.Logger
}

func NewUploader(fs fileStorage, log *slog.Logger) uploader.Uploader {
	return polygonUploader{
		fs:  fs,
		log: log,
	}
}

func (u polygonUploader) SupportsFormat(format string) bool {
	return strings.EqualFold(format, uploader.FormatPolygon)
}

func (u polygonUploader) Upload(ctx context.Context, cfg uploader.Config) (task.ID, error) {
	u.info("polygon upload started",
		slog.String("src", cfg.SrcPath),
		slog.Int("level", cfg.Level),
	)

	if cfg.SrcPath == "" {
		return task.ID{}, errors.New("missing source path")
	}
	if cfg.Level < 1 || cfg.Level > 10 {
		return task.ID{}, errors.New("level must be in range [1..10]")
	}
	if u.fs == nil {
		return task.ID{}, errors.New("file storage is not configured")
	}

	pkgDir, cleanup, err := prepareSourceDir(cfg.SrcPath)
	if err != nil {
		return task.ID{}, fmt.Errorf("failed to prepare source: %w", err)
	}
	defer cleanup()
	u.info("source prepared", slog.String("package_dir", pkgDir))

	problemPath := filepath.Join(pkgDir, "problem.xml")
	problem, err := loadProblem(problemPath)
	if err != nil {
		return task.ID{}, fmt.Errorf("failed to parse problem.xml: %w", err)
	}
	bucketID, err := taskIDFromShortName(problem.Short)
	if err != nil {
		return task.ID{}, err
	}
	u.info("problem parsed", slog.String("task_id", bucketID))

	testset, err := pickTestset(problem)
	if err != nil {
		return task.ID{}, fmt.Errorf("failed to select testset: %w", err)
	}

	title := pickTitle(problem)
	if title == "" {
		return task.ID{}, errors.New("failed to extract task title from problem.xml")
	}
	u.info("metadata extracted",
		slog.String("title", title),
		slog.Int("time_limit_ms", testset.TimeLimit),
		slog.Int("memory_limit_bytes", testset.MemoryLimit),
	)

	statementPath := pickStatementPath(problem)
	if statementPath == "" {
		statementPath = filepath.Join("statements", "russian", "problem.tex")
	}
	statementAbs := filepath.Join(pkgDir, filepath.Clean(statementPath))

	solutionSource := pickSolutionSource(problem)
	solutionRel := strings.TrimSpace(solutionSource.Path)
	if solutionRel == "" {
		return task.ID{}, errors.New("failed to extract main solution path from problem.xml")
	}
	solutionLang, err := detectLanguage(solutionSource.Type, solutionRel)
	if err != nil {
		return task.ID{}, fmt.Errorf("failed to detect solution language: %w", err)
	}
	checkerSource := problem.Assets.Checker.Source
	checkerRel := strings.TrimSpace(checkerSource.Path)
	if checkerRel == "" {
		return task.ID{}, errors.New("failed to extract checker path from problem.xml")
	}
	checkerLang, err := detectLanguage(checkerSource.Type, checkerRel)
	if err != nil {
		return task.ID{}, fmt.Errorf("failed to detect checker language: %w", err)
	}
	if checkerLang != task.LanguageCpp {
		return task.ID{}, fmt.Errorf("unsupported checker language: %s (only Cpp is allowed)", checkerLang)
	}
	solutionFileName := buildCodeOutputName("solution", solutionRel, solutionLang)
	checkerFileName := buildCodeOutputName("checker", checkerRel, checkerLang)

	var reservedBucketID bucket.ID
	if err = reservedBucketID.FromString(bucketID); err != nil {
		return task.ID{}, fmt.Errorf("failed to parse bucket id: %w", err)
	}

	outDir, commit, abort, err := u.fs.ReserveBucket(ctx, reservedBucketID, nil)
	if err != nil {
		return task.ID{}, err
	}
	u.info("bucket reserved", slog.String("path", outDir))
	committed := false
	defer func() {
		if !committed {
			_ = abort()
		}
	}()

	statementMD, err := buildStatementMarkdown(statementAbs, title)
	if err != nil {
		return task.ID{}, fmt.Errorf("failed to convert statement: %w", err)
	}
	if err = writeFile(filepath.Join(outDir, "statement.md"), []byte(statementMD)); err != nil {
		return task.ID{}, fmt.Errorf("failed to write statement.md: %w", err)
	}
	u.info("statement saved")

	solutionAbs := filepath.Join(pkgDir, filepath.Clean(solutionRel))
	solutionCode, err := os.ReadFile(solutionAbs)
	if err != nil {
		return task.ID{}, fmt.Errorf("failed to read solution %s: %w", solutionRel, err)
	}
	if err = writeFile(filepath.Join(outDir, solutionFileName), solutionCode); err != nil {
		return task.ID{}, fmt.Errorf("failed to write %s: %w", solutionFileName, err)
	}
	u.info("solution saved",
		slog.String("path", solutionFileName),
		slog.String("lang", string(solutionLang)),
	)

	checkerAbs := filepath.Join(pkgDir, filepath.Clean(checkerRel))
	checkerCode, err := os.ReadFile(checkerAbs)
	if err != nil {
		return task.ID{}, fmt.Errorf("failed to read checker %s: %w", checkerRel, err)
	}
	if err = writeFile(filepath.Join(outDir, checkerFileName), checkerCode); err != nil {
		return task.ID{}, fmt.Errorf("failed to write %s: %w", checkerFileName, err)
	}
	u.info("checker saved",
		slog.String("path", checkerFileName),
		slog.String("lang", string(checkerLang)),
	)

	testsDir := filepath.Join(outDir, "tests")
	if err = os.MkdirAll(testsDir, 0o777); err != nil {
		return task.ID{}, fmt.Errorf("failed to create tests directory: %w", err)
	}

	taskTests, missingOutputs, err := copyTests(pkgDir, testset, testsDir)
	if err != nil {
		return task.ID{}, fmt.Errorf("failed to copy tests: %w", err)
	}
	u.info("tests copied",
		slog.Int("count", len(taskTests)),
		slog.Int("missing_outputs", len(missingOutputs)),
	)

	if len(missingOutputs) > 0 {
		if err = generateMissingOutputs(solutionAbs, solutionLang, testsDir, missingOutputs); err != nil {
			return task.ID{}, fmt.Errorf("failed to generate missing outputs: %w", err)
		}
		u.info("missing outputs generated", slog.Int("count", len(missingOutputs)))
	}

	var taskID task.ID
	if err = taskID.FromString(bucketID); err != nil {
		return task.ID{}, fmt.Errorf("failed to convert bucket id to task id: %w", err)
	}
	taskModel := tasks.WriteCodeTask{
		Details: task.Details{
			ID:        taskID,
			Title:     title,
			Type:      task.WriteCode,
			Level:     task.Level(cfg.Level),
			Topics:    []string{},
			Statement: "statement.md",
		},
		TimeLimit:   testset.TimeLimit,
		MemoryLimit: memoryBytesToMB(testset.MemoryLimit),
		Tests:       taskTests,
		Checker: task.Code{
			Path: checkerFileName,
			Lang: checkerLang,
		},
		Solution: task.Code{
			Path: solutionFileName,
			Lang: solutionLang,
		},
	}
	taskBytes, err := json.MarshalIndent(taskModel, "", "    ")
	if err != nil {
		return task.ID{}, fmt.Errorf("failed to marshal task.json: %w", err)
	}
	if err = writeFile(filepath.Join(outDir, "task.json"), taskBytes); err != nil {
		return task.ID{}, fmt.Errorf("failed to write task.json: %w", err)
	}
	u.info("task.json saved")

	if err = commit(); err != nil {
		return task.ID{}, fmt.Errorf("failed to commit bucket: %w", err)
	}
	committed = true
	u.info("bucket committed", slog.String("task_id", taskID.String()))

	return taskID, nil
}

func (u polygonUploader) info(msg string, attrs ...any) {
	if u.log == nil {
		return
	}
	u.log.Info(msg, attrs...)
}

func prepareSourceDir(src string) (string, func(), error) {
	stat, err := os.Stat(src)
	if err != nil {
		return "", nil, err
	}
	if stat.IsDir() {
		return src, func() {}, nil
	}

	if strings.EqualFold(filepath.Ext(src), ".zip") {
		tempDir, err := os.MkdirTemp("", "polygon_pkg_*")
		if err != nil {
			return "", nil, err
		}
		if err = unzip(src, tempDir); err != nil {
			_ = os.RemoveAll(tempDir)
			return "", nil, err
		}
		root := pickZipRoot(tempDir)
		return root, func() { _ = os.RemoveAll(tempDir) }, nil
	}

	return "", nil, fmt.Errorf("unsupported source type: %s (expected directory or .zip)", src)
}

func unzip(zipPath, dest string) error {
	r, err := zip.OpenReader(zipPath)
	if err != nil {
		return err
	}
	defer func() { _ = r.Close() }()

	for _, f := range r.File {
		target := filepath.Join(dest, f.Name)
		if !strings.HasPrefix(filepath.Clean(target), filepath.Clean(dest)+string(os.PathSeparator)) &&
			filepath.Clean(target) != filepath.Clean(dest) {
			return fmt.Errorf("zip contains unsafe path: %s", f.Name)
		}

		if f.FileInfo().IsDir() {
			if err := os.MkdirAll(target, 0o777); err != nil {
				return err
			}
			continue
		}

		if err := os.MkdirAll(filepath.Dir(target), 0o777); err != nil {
			return err
		}
		rc, err := f.Open()
		if err != nil {
			return err
		}
		data, err := io.ReadAll(rc)
		_ = rc.Close()
		if err != nil {
			return err
		}
		if err := os.WriteFile(target, data, 0o666); err != nil {
			return err
		}
	}

	return nil
}

func pickZipRoot(dir string) string {
	entries, err := os.ReadDir(dir)
	if err != nil || len(entries) != 1 || !entries[0].IsDir() {
		return dir
	}
	candidate := filepath.Join(dir, entries[0].Name())
	if _, err := os.Stat(filepath.Join(candidate, "problem.xml")); err == nil {
		return candidate
	}
	return dir
}

func loadProblem(path string) (polygonProblem, error) {
	var problem polygonProblem
	data, err := os.ReadFile(path)
	if err != nil {
		return problem, err
	}
	if err = xml.Unmarshal(data, &problem); err != nil {
		return problem, err
	}
	return problem, nil
}

func pickTitle(p polygonProblem) string {
	for _, n := range p.Names.Names {
		if strings.EqualFold(n.Language, "russian") {
			return strings.TrimSpace(n.Value)
		}
	}
	if len(p.Names.Names) > 0 {
		return strings.TrimSpace(p.Names.Names[0].Value)
	}
	return ""
}

func pickStatementPath(p polygonProblem) string {
	for _, s := range p.Statements.Statements {
		if strings.EqualFold(s.Language, "russian") && strings.HasSuffix(strings.ToLower(s.Path), ".tex") {
			return s.Path
		}
	}
	for _, s := range p.Statements.Statements {
		if strings.HasSuffix(strings.ToLower(s.Path), ".tex") {
			return s.Path
		}
	}
	return ""
}

func pickSolutionSource(p polygonProblem) polygonSource {
	for _, s := range p.Assets.Solutions.Solutions {
		if strings.EqualFold(s.Tag, "main") {
			return s.Source
		}
	}
	if len(p.Assets.Solutions.Solutions) > 0 {
		return p.Assets.Solutions.Solutions[0].Source
	}
	return polygonSource{}
}

func pickTestset(p polygonProblem) (polygonTestset, error) {
	if len(p.Judging.Testsets) == 0 {
		return polygonTestset{}, errors.New("no testset found")
	}
	for _, ts := range p.Judging.Testsets {
		if ts.Name == "tests" {
			return ts, nil
		}
	}
	return p.Judging.Testsets[0], nil
}

func copyTests(pkgDir string, ts polygonTestset, testsDir string) ([]task.Test, []int, error) {
	count := ts.TestCount
	if len(ts.Tests.Tests) > 0 {
		count = len(ts.Tests.Tests)
	}
	if count <= 0 {
		return nil, nil, errors.New("test count is zero")
	}

	taskTests := make([]task.Test, 0, count)
	missingOutputs := make([]int, 0)

	for i := 1; i <= count; i++ {
		destInputRel := fmt.Sprintf("tests/%02d.in", i)
		destOutputRel := fmt.Sprintf("tests/%02d.out", i)
		destInputAbs := filepath.Join(testsDir, fmt.Sprintf("%02d.in", i))
		destOutputAbs := filepath.Join(testsDir, fmt.Sprintf("%02d.out", i))

		inputSrc, err := resolveTestInputPath(pkgDir, ts.InputPathPattern, i)
		if err != nil {
			return nil, nil, fmt.Errorf("test %d input: %w", i, err)
		}
		inputData, err := os.ReadFile(inputSrc)
		if err != nil {
			return nil, nil, fmt.Errorf("test %d input read: %w", i, err)
		}
		if err = writeFile(destInputAbs, inputData); err != nil {
			return nil, nil, fmt.Errorf("test %d input write: %w", i, err)
		}

		outputSrc, found, err := resolveTestOutputPath(pkgDir, ts.AnswerPathPattern, i)
		if err != nil {
			return nil, nil, fmt.Errorf("test %d output: %w", i, err)
		}
		if found {
			outputData, err := os.ReadFile(outputSrc)
			if err != nil {
				return nil, nil, fmt.Errorf("test %d output read: %w", i, err)
			}
			if err = writeFile(destOutputAbs, outputData); err != nil {
				return nil, nil, fmt.Errorf("test %d output write: %w", i, err)
			}
		} else {
			missingOutputs = append(missingOutputs, i)
		}

		visible := false
		if i <= len(ts.Tests.Tests) {
			visible = strings.EqualFold(strings.TrimSpace(ts.Tests.Tests[i-1].Sample), "true")
		}
		taskTests = append(taskTests, task.Test{
			ID:      i,
			Input:   destInputRel,
			Output:  destOutputRel,
			Visible: visible,
		})
	}

	return taskTests, missingOutputs, nil
}

func resolveTestInputPath(pkgDir, pattern string, i int) (string, error) {
	candidates := make([]string, 0, 5)
	if strings.TrimSpace(pattern) != "" {
		candidates = append(candidates, filepath.Join(pkgDir, fmt.Sprintf(pattern, i)))
	}
	candidates = append(candidates,
		filepath.Join(pkgDir, "tests", fmt.Sprintf("%02d", i)),
		filepath.Join(pkgDir, "tests", strconv.Itoa(i)),
		filepath.Join(pkgDir, "tests", fmt.Sprintf("%02d.in", i)),
		filepath.Join(pkgDir, "tests", fmt.Sprintf("%d.in", i)),
	)
	for _, p := range candidates {
		if fileExists(p) {
			return p, nil
		}
	}
	return "", fmt.Errorf("input file not found (tested %d candidates)", len(candidates))
}

func resolveTestOutputPath(pkgDir, pattern string, i int) (string, bool, error) {
	candidates := make([]string, 0, 5)
	if strings.TrimSpace(pattern) != "" {
		candidates = append(candidates, filepath.Join(pkgDir, fmt.Sprintf(pattern, i)))
	}
	candidates = append(candidates,
		filepath.Join(pkgDir, "tests", fmt.Sprintf("%02d.a", i)),
		filepath.Join(pkgDir, "tests", strconv.Itoa(i)+".a"),
		filepath.Join(pkgDir, "tests", fmt.Sprintf("%02d.out", i)),
		filepath.Join(pkgDir, "tests", fmt.Sprintf("%d.out", i)),
	)
	for _, p := range candidates {
		if fileExists(p) {
			return p, true, nil
		}
	}
	return "", false, nil
}

func generateMissingOutputs(solutionPath string, solutionLang task.Language, testsDir string, missing []int) error {
	run := func(_ context.Context, _ []byte) ([]byte, string, error) {
		return nil, "", nil
	}
	cleanup := func() {}

	switch solutionLang {
	case task.LanguageCpp:
		bin, done, err := compileCppSolution(solutionPath)
		if err != nil {
			return err
		}
		cleanup = done
		run = func(ctx context.Context, in []byte) ([]byte, string, error) {
			cmd := exec.CommandContext(ctx, bin)
			cmd.Stdin = bytes.NewReader(in)
			var stdout, stderr bytes.Buffer
			cmd.Stdout = &stdout
			cmd.Stderr = &stderr
			err := cmd.Run()
			return stdout.Bytes(), strings.TrimSpace(stderr.String()), err
		}
	case task.LanguageGo:
		bin, done, err := compileGoSolution(solutionPath)
		if err != nil {
			return err
		}
		cleanup = done
		run = func(ctx context.Context, in []byte) ([]byte, string, error) {
			cmd := exec.CommandContext(ctx, bin)
			cmd.Stdin = bytes.NewReader(in)
			var stdout, stderr bytes.Buffer
			cmd.Stdout = &stdout
			cmd.Stderr = &stderr
			err := cmd.Run()
			return stdout.Bytes(), strings.TrimSpace(stderr.String()), err
		}
	case task.LanguagePython:
		run = func(ctx context.Context, in []byte) ([]byte, string, error) {
			cmd := exec.CommandContext(ctx, "python3", solutionPath)
			cmd.Stdin = bytes.NewReader(in)
			var stdout, stderr bytes.Buffer
			cmd.Stdout = &stdout
			cmd.Stderr = &stderr
			err := cmd.Run()
			return stdout.Bytes(), strings.TrimSpace(stderr.String()), err
		}
	default:
		return fmt.Errorf("missing outputs generation is not supported for language %s", solutionLang)
	}
	defer cleanup()

	for _, i := range missing {
		inPath := filepath.Join(testsDir, fmt.Sprintf("%02d.in", i))
		outPath := filepath.Join(testsDir, fmt.Sprintf("%02d.out", i))

		inData, err := os.ReadFile(inPath)
		if err != nil {
			return fmt.Errorf("test %d input read for generation: %w", i, err)
		}

		ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
		stdout, stderr, err := run(ctx, inData)
		cancel()
		if err != nil {
			return fmt.Errorf("test %d output generation failed: %w; stderr: %s", i, err, stderr)
		}
		if err = writeFile(outPath, stdout); err != nil {
			return fmt.Errorf("test %d output write after generation: %w", i, err)
		}
	}

	return nil
}

func compileCppSolution(src string) (string, func(), error) {
	binPath := filepath.Join(os.TempDir(), fmt.Sprintf("polygon_solution_%d", time.Now().UnixNano()))
	cmd := exec.Command("g++", "-std=c++17", "-O2", src, "-o", binPath)
	var stderr bytes.Buffer
	cmd.Stderr = &stderr
	if err := cmd.Run(); err != nil {
		return "", nil, fmt.Errorf("failed to compile solution with g++: %w; stderr: %s", err, strings.TrimSpace(stderr.String()))
	}
	return binPath, func() { _ = os.Remove(binPath) }, nil
}

func compileGoSolution(src string) (string, func(), error) {
	binPath := filepath.Join(os.TempDir(), fmt.Sprintf("polygon_solution_%d", time.Now().UnixNano()))
	cmd := exec.Command("go", "build", "-o", binPath, src)
	var stderr bytes.Buffer
	cmd.Stderr = &stderr
	if err := cmd.Run(); err != nil {
		return "", nil, fmt.Errorf("failed to compile solution with go build: %w; stderr: %s", err, strings.TrimSpace(stderr.String()))
	}
	return binPath, func() { _ = os.Remove(binPath) }, nil
}

func inlineTestlibIfNeeded(pkgDir, checkerAbs string, checkerCode []byte) ([]byte, error) {
	code := string(checkerCode)
	includeRe := regexp.MustCompile(`(?m)^\s*#\s*include\s*[<"]testlib\.h[>"]\s*$`)
	if !includeRe.MatchString(code) {
		return checkerCode, nil
	}

	testlibCandidates := []string{
		filepath.Join(filepath.Dir(checkerAbs), "testlib.h"),
		filepath.Join(pkgDir, "files", "testlib.h"),
	}
	var testlibData []byte
	for _, p := range testlibCandidates {
		if fileExists(p) {
			data, err := os.ReadFile(p)
			if err != nil {
				return nil, fmt.Errorf("failed to read testlib.h at %s: %w", p, err)
			}
			testlibData = data
			break
		}
	}
	if len(testlibData) == 0 {
		return nil, errors.New("checker includes testlib.h, but file was not found")
	}

	codeNoInclude := includeRe.ReplaceAllString(code, "")
	out := strings.TrimRight(string(testlibData), "\r\n") + "\n\n" + strings.TrimLeft(codeNoInclude, "\r\n")
	return []byte(out), nil
}

func buildStatementMarkdown(statementTexPath, title string) (string, error) {
	data, err := os.ReadFile(statementTexPath)
	if err != nil {
		return "", err
	}
	src := string(data)
	src = strings.ReplaceAll(src, "\r\n", "\n")

	legend := extractBetween(src, `\begin{problem}`, `\InputFile`)
	input := extractBetween(src, `\InputFile`, `\OutputFile`)
	output := extractBetween(src, `\OutputFile`, `\Example`)
	if output == "" {
		output = extractBetween(src, `\OutputFile`, `\end{problem}`)
	}
	legend = stripProblemHeaderArgs(legend)

	legend = cleanLatexText(legend)
	input = cleanLatexText(input)
	output = cleanLatexText(output)

	var b strings.Builder
	b.WriteString("# ")
	b.WriteString(strings.TrimSpace(title))
	b.WriteString("\n\n")
	if legend != "" {
		b.WriteString("## Условие\n\n")
		b.WriteString(strings.TrimSpace(legend))
		b.WriteString("\n\n")
	}
	if input != "" {
		b.WriteString("## Формат входных данных\n\n")
		b.WriteString(strings.TrimSpace(input))
		b.WriteString("\n\n")
	}
	if output != "" {
		b.WriteString("## Формат выходных данных\n\n")
		b.WriteString(strings.TrimSpace(output))
		b.WriteString("\n")
	}
	return strings.TrimSpace(b.String()) + "\n", nil
}

func extractBetween(s, left, right string) string {
	l := strings.Index(s, left)
	if l < 0 {
		return ""
	}
	s = s[l+len(left):]
	r := strings.Index(s, right)
	if r < 0 {
		return s
	}
	return s[:r]
}

func cleanLatexText(s string) string {
	s = strings.TrimSpace(s)
	s = strings.ReplaceAll(s, "``", "\"")
	s = strings.ReplaceAll(s, "''", "\"")
	s = strings.ReplaceAll(s, "<<", "«")
	s = strings.ReplaceAll(s, ">>", "»")
	s = strings.ReplaceAll(s, `\~`, " ")
	s = strings.ReplaceAll(s, `\_`, "_")
	s = strings.ReplaceAll(s, `\%`, "%")
	s = strings.ReplaceAll(s, `\#`, "#")
	s = strings.ReplaceAll(s, `\&`, "&")
	s = strings.ReplaceAll(s, `\{`, "{")
	s = strings.ReplaceAll(s, `\}`, "}")
	s = strings.ReplaceAll(s, `\leq`, "≤")
	s = strings.ReplaceAll(s, `\le`, "≤")
	s = strings.ReplaceAll(s, `\geq`, "≥")
	s = strings.ReplaceAll(s, `\ge`, "≥")
	s = strings.ReplaceAll(s, `\cdot`, "·")
	s = strings.ReplaceAll(s, `\times`, "×")
	s = strings.ReplaceAll(s, `\ldots`, "...")
	s = strings.ReplaceAll(s, `\dots`, "...")
	s = strings.ReplaceAll(s, `\texttt`, "")
	s = strings.ReplaceAll(s, `\textbf`, "")
	s = strings.ReplaceAll(s, `\emph`, "")
	s = strings.ReplaceAll(s, "{", "")
	s = strings.ReplaceAll(s, "}", "")
	s = strings.ReplaceAll(s, `\par`, "\n")
	s = strings.ReplaceAll(s, `\\`, "\n")
	lines := strings.Split(s, "\n")
	out := make([]string, 0, len(lines))
	for _, line := range lines {
		line = strings.TrimSpace(line)
		if line == "" {
			if len(out) > 0 && out[len(out)-1] != "" {
				out = append(out, "")
			}
			continue
		}
		if strings.HasPrefix(line, `\begin{`) || strings.HasPrefix(line, `\end{`) || strings.HasPrefix(line, `\exmpfile`) {
			continue
		}
		out = append(out, line)
	}
	return strings.TrimSpace(strings.Join(out, "\n"))
}

func stripProblemHeaderArgs(s string) string {
	s = strings.TrimLeft(s, " \t\r\n")
	re := regexp.MustCompile(`^\{[^{}]*\}\{[^{}]*\}\{[^{}]*\}\{[^{}]*\}\{[^{}]*\}\s*`)
	return re.ReplaceAllString(s, "")
}

func writeFile(path string, data []byte) error {
	data = ensureTrailingNewline(data)
	if err := os.MkdirAll(filepath.Dir(path), 0o777); err != nil {
		return err
	}
	return os.WriteFile(path, data, 0o666)
}

func ensureTrailingNewline(data []byte) []byte {
	if len(data) == 0 {
		return []byte{'\n'}
	}
	if data[len(data)-1] == '\n' {
		return data
	}
	return append(data, '\n')
}

func detectLanguage(sourceType, path string) (task.Language, error) {
	kind := strings.ToLower(strings.TrimSpace(sourceType))
	ext := strings.ToLower(filepath.Ext(path))
	switch {
	case strings.Contains(kind, "cpp"), strings.Contains(kind, "g++"), ext == ".cpp", ext == ".cc", ext == ".cxx":
		return task.LanguageCpp, nil
	case strings.Contains(kind, "python"), strings.Contains(kind, "py"), ext == ".py":
		return task.LanguagePython, nil
	case strings.Contains(kind, "go"), ext == ".go":
		return task.LanguageGo, nil
	default:
		return "", fmt.Errorf("unsupported language type %q for %s", sourceType, path)
	}
}

func buildCodeOutputName(prefix, sourcePath string, lang task.Language) string {
	ext := strings.ToLower(filepath.Ext(sourcePath))
	if ext == "" {
		switch lang {
		case task.LanguageCpp:
			ext = ".cpp"
		case task.LanguagePython:
			ext = ".py"
		case task.LanguageGo:
			ext = ".go"
		}
	}
	return prefix + ext
}

func memoryBytesToMB(bytes int) int {
	if bytes <= 0 {
		return 0
	}
	return bytes / (1024 * 1024)
}

func taskIDFromShortName(shortName string) (string, error) {
	shortName = strings.TrimSpace(shortName)
	if shortName == "" {
		return "", errors.New("problem.xml short-name is empty")
	}
	hash := sha1.Sum([]byte(shortName))
	return hex.EncodeToString(hash[:]), nil
}

func fileExists(path string) bool {
	_, err := os.Stat(path)
	return err == nil
}
