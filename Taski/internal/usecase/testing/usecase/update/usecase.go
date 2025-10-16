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

		var msg testing.Message
		switch t.GetType() {
		case task.WriteCode:
			switch command.Event.GetType() {
			case testing.StartExecutionEvent:
				fmt.Printf("[%s] started testing\n", sol.ID)
				msg = messages.NewStartTestingMessage(sol.ID)
			case testing.CompileStepEvent:
				event := command.Event.(*events.CompileStepEvent)
				switch event.CompileStatus {
				case events.CompileStatusOK:
					fmt.Printf("[%s] %s step OK\n", sol.ID, event.StepName)
					if strings.Contains(event.StepName, "suspect") {
						msg = messages.NewUpdateStatusMessage(sol.ID, "Compiled successfully")
					}
				case events.CompileStatusCE:
					fmt.Printf("[%s] %s step CE\n", sol.ID, event.StepName)
					if strings.Contains(event.StepName, "suspect") {
						msg = messages.NewFinishTestingMessageWithMessage(sol.ID, "Compilation Error", event.Error)
					} else {
						msg = messages.NewFinishTestingMessageWithError(sol.ID, "Testing failed")
					}
				default:
					return fmt.Errorf("unknown compile status: %s", event.CompileStatus)
				}
			case testing.RunStepEvent:
				event := command.Event.(*events.RunStepEvent)
				testID, err := strconv.Atoi(event.StepName[strings.LastIndex(event.StepName, "_")+1:])
				if err != nil {
					return fmt.Errorf("failed to parse test id: %s", err)
				}
				prevTestedPrefix := sol.GetTestedPrefix()
				switch event.RunStatus {
				case events.RunStatusOK:
					fmt.Printf("[%s] %s step OK\n", sol.ID, event.StepName)
				case events.RunStatusTL:
					fmt.Printf("[%s] %s step TL\n", sol.ID, event.StepName)
					if strings.Contains(event.StepName, "suspect") {
						sol.Status[testID] = "-"
						testedPrefix := sol.GetTestedPrefix()
						if prevTestedPrefix < testedPrefix {
							msg = messages.NewFinishTestingMessage(sol.ID, fmt.Sprintf("Time Limit on test %d", testID))
						}
					} else {
						msg = messages.NewFinishTestingMessageWithError(sol.ID, "Testing failed")
					}
				case events.RunStatusML:
					fmt.Printf("[%s] %s step ML\n", sol.ID, event.StepName)
					if strings.Contains(event.StepName, "suspect") {
						sol.Status[testID] = "-"
						testedPrefix := sol.GetTestedPrefix()
						if prevTestedPrefix < testedPrefix {
							msg = messages.NewFinishTestingMessage(sol.ID, fmt.Sprintf("Memory Limit on test %d", testID))
						}
					} else {
						msg = messages.NewFinishTestingMessageWithError(sol.ID, "Testing failed")
					}
				case events.RunStatusRE:
					fmt.Printf("[%s] %s step RE\n", sol.ID, event.StepName)
					if strings.Contains(event.StepName, "suspect") {
						sol.Status[testID] = "-"
						testedPrefix := sol.GetTestedPrefix()
						if prevTestedPrefix < testedPrefix {
							msg = messages.NewFinishTestingMessage(sol.ID, fmt.Sprintf("Runtime error on test %d", testID))
						}
					} else {
						msg = messages.NewFinishTestingMessageWithError(sol.ID, "Testing failed")
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
				prevTestedPrefix := sol.GetTestedPrefix()
				switch event.CheckStatus {
				case events.CheckStatusOK:
					fmt.Printf("[%s] %s step OK\n", sol.ID, event.StepName)
					sol.Status[testID] = "+"
					testedPrefix := sol.GetTestedPrefix()
					if prevTestedPrefix < testedPrefix {
						msg = messages.NewUpdateStatusMessage(sol.ID, fmt.Sprintf("Test %d passed", testID))
					}
				case events.CheckStatusWA:
					fmt.Printf("[%s] %s step WA\n", sol.ID, event.StepName)
					sol.Status[testID] = "-"
					testedPrefix := sol.GetTestedPrefix()
					if prevTestedPrefix < testedPrefix {
						msg = messages.NewFinishTestingMessage(sol.ID, fmt.Sprintf("Wrong answer on test %d", testID))
					}
				default:
					return fmt.Errorf("unknown run status: %s", event.CheckStatus)
				}
			case testing.FinishExecutionEvent:
				event := command.Event.(*events.FinishExecutionEvent)
				if event.Error != "" {
					fmt.Printf("[%s] finished testing with error: %s\n", sol.ID, event.Error)
					msg = messages.NewFinishTestingMessageWithError(sol.ID, event.Error)
				} else {
					fmt.Printf("[%s] finished testing\n", sol.ID)
					if sol.AllTestsPassed() {
						msg = messages.NewFinishTestingMessage(sol.ID, "Accepted")
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

		if err = uc.solutionStorage.Update(ctx, sol); err != nil {
			return fmt.Errorf("failed to update solution in storage: %w", err)
		}

		return nil
	})
}
