package update

import (
	"context"
	"fmt"
	"log/slog"
	"strconv"
	"strings"
	"taski/internal/domain/task"
	"taski/internal/domain/testing"
	"taski/internal/domain/testing/events"
	"taski/internal/domain/testing/messages"
	"time"
)

type (
	Command struct {
		Event testing.Event
	}

	UseCase struct {
		log *slog.Logger

		unitOfWork      unitOfWork
		solutionStorage solutionStorage

		taskStorage taskStorage

		messageProducer messageProducer
	}

	unitOfWork interface {
		Do(context.Context, func(ctx context.Context) error) error
	}

	solutionStorage interface {
		GetByExecutionID(context.Context, testing.ExecutionID) (testing.Solution, error)
		Update(context.Context, testing.Solution) error
	}

	taskStorage interface {
		Get(task.ID) (task task.Task, unlock func(), err error)
	}

	messageProducer interface {
		Produce(ctx context.Context, msg testing.Message) error
	}
)

func NewUseCase(log *slog.Logger, solutionStorage solutionStorage, unitOfWork unitOfWork, taskStorage taskStorage, messageProducer messageProducer) *UseCase {
	return &UseCase{
		log: log,

		unitOfWork:      unitOfWork,
		solutionStorage: solutionStorage,

		taskStorage: taskStorage,

		messageProducer: messageProducer,
	}
}

func (uc *UseCase) Update(ctx context.Context, command Command) error {
	return uc.unitOfWork.Do(ctx, func(ctx context.Context) error {
		sol, err := uc.solutionStorage.GetByExecutionID(ctx, command.Event.GetExecutionID())
		if err != nil {
			return fmt.Errorf("failed to get solution by execution id: %w", err)
		}

		t, unlock, err := uc.taskStorage.Get(sol.TaskID)
		if err != nil {
			uc.log.Error("failed to get task from storage", slog.Any("err", err))
			return fmt.Errorf("failed to get task from storage")
		}
		defer unlock()

		finished := false

		var msg testing.Message
		switch t.GetType() {
		case task.WriteCode:
			switch command.Event.GetType() {
			case testing.StartExecutionEvent:
				uc.log.Info("started testing", slog.Any("sol_id", sol.ID))
				msg = messages.NewStartTestingMessage(sol.ID)
			case testing.CompileStepEvent:
				event := command.Event.(*events.CompileStepEvent)
				uc.log.Info("processed compile step",
					slog.Any("sol_id", sol.ID),
					slog.Any("step", event.StepName),
					slog.Any("status", event.CompileStatus),
				)
				switch event.CompileStatus {
				case events.CompileStatusOK:
					if strings.Contains(event.StepName, "suspect") {
						msg = messages.NewUpdateStatusMessage(sol.ID, "Compiled successfully")
					}
				case events.CompileStatusCE:
					if strings.Contains(event.StepName, "suspect") {
						msg = messages.NewFinishTestingMessageWithMessage(sol.ID, "Compilation Error", event.Error)
					} else {
						msg = messages.NewFinishTestingMessageWithError(sol.ID, "Testing failed")
					}
					finished = true
				default:
					return fmt.Errorf("unknown compile status: %s", event.CompileStatus)
				}
			case testing.RunStepEvent:
				event := command.Event.(*events.RunStepEvent)

				testID, err := strconv.Atoi(event.StepName[strings.LastIndex(event.StepName, "_")+1:])
				if err != nil {
					return fmt.Errorf("failed to parse test id: %s", err)
				}

				uc.log.Info("processed run step",
					slog.Any("sol_id", sol.ID),
					slog.Any("step", event.StepName),
					slog.Any("status", event.RunStatus),
				)

				prevTestedPrefix := sol.GetTestedPrefix()
				switch event.RunStatus {
				case events.RunStatusOK:
				case events.RunStatusTL:
					sol.Status[testID] = "-"
					if strings.Contains(event.StepName, "suspect") {
						testedPrefix := sol.GetTestedPrefix()
						if prevTestedPrefix < testedPrefix {
							msg = messages.NewFinishTestingMessage(sol.ID, fmt.Sprintf("Time Limit on test %d", testID))
							finished = true
						}
					} else {
						msg = messages.NewFinishTestingMessageWithError(sol.ID, "Testing failed")
						finished = true
					}
				case events.RunStatusML:
					sol.Status[testID] = "-"
					if strings.Contains(event.StepName, "suspect") {
						testedPrefix := sol.GetTestedPrefix()
						if prevTestedPrefix < testedPrefix {
							msg = messages.NewFinishTestingMessage(sol.ID, fmt.Sprintf("Memory Limit on test %d", testID))
							finished = true
						}
					} else {
						msg = messages.NewFinishTestingMessageWithError(sol.ID, "Testing failed")
						finished = true
					}
				case events.RunStatusRE:
					sol.Status[testID] = "-"
					if strings.Contains(event.StepName, "suspect") {
						testedPrefix := sol.GetTestedPrefix()
						if prevTestedPrefix < testedPrefix {
							msg = messages.NewFinishTestingMessage(sol.ID, fmt.Sprintf("Runtime error on test %d", testID))
							finished = true
						}
					} else {
						msg = messages.NewFinishTestingMessageWithError(sol.ID, "Testing failed")
						finished = true
					}
				default:
					return fmt.Errorf("unknown run status: %s", event.RunStatus)
				}
			case testing.CheckStepEvent:
				event := command.Event.(*events.CheckStepEvent)

				testID, err := strconv.Atoi(event.StepName[strings.LastIndex(event.StepName, "_")+1:])
				if err != nil {
					return fmt.Errorf("failed to parse test id: %s", err)
				}

				uc.log.Info("processed check step",
					slog.Any("sol_id", sol.ID),
					slog.Any("step", event.StepName),
					slog.Any("status", event.CheckStatus),
				)

				prevTestedPrefix := sol.GetTestedPrefix()
				switch event.CheckStatus {
				case events.CheckStatusOK:
					sol.Status[testID] = "+"
					testedPrefix := sol.GetTestedPrefix()
					if prevTestedPrefix < testedPrefix {
						msg = messages.NewUpdateStatusMessage(sol.ID, fmt.Sprintf("Test %d passed", testID))
					}
				case events.CheckStatusWA:
					sol.Status[testID] = "-"
					testedPrefix := sol.GetTestedPrefix()
					if prevTestedPrefix < testedPrefix {
						msg = messages.NewFinishTestingMessage(sol.ID, fmt.Sprintf("Wrong answer on test %d", testID))
						finished = true
					}
				default:
					return fmt.Errorf("unknown run status: %s", event.CheckStatus)
				}
			case testing.FinishExecutionEvent:
				event := command.Event.(*events.FinishExecutionEvent)

				uc.log.Info("finished testing", slog.Any("sol_id", sol.ID))

				if event.Error != "" {
					msg = messages.NewFinishTestingMessageWithError(sol.ID, event.Error)
					finished = true
				} else {
					if sol.AllTestsPassed() {
						msg = messages.NewFinishTestingMessage(sol.ID, "Accepted")
						finished = true
					}
				}
			default:
				return fmt.Errorf("unknown event type: %s", command.Event.GetType())
			}

			if msg != nil {
				if err = uc.messageProducer.Produce(ctx, msg); err != nil {
					return fmt.Errorf("failed to produce message: %w", err)
				}
			}
		default:
			return fmt.Errorf("cannot work with task of type %s", t.GetType())
		}

		if finished && sol.FinishedAt == nil {
			finishedAt := time.Now()
			sol.FinishedAt = &finishedAt
		}

		if err = uc.solutionStorage.Update(ctx, sol); err != nil {
			return fmt.Errorf("failed to update solution in storage: %w", err)
		}

		return nil
	})
}
