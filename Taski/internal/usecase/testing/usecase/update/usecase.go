package update

import (
	"context"
	"errors"
	"fmt"
	"log/slog"
	"taski/internal/domain/testing"
	"taski/internal/domain/testing/event"
	"taski/internal/domain/testing/event/events"
	"taski/internal/domain/testing/execution"
	"taski/internal/domain/testing/job"
	"taski/internal/domain/testing/message/messages"
	"taski/internal/storage/postgres"
	"time"
)

type (
	Command struct {
		Event events.Event
	}

	UseCase struct {
		log *slog.Logger

		unitOfWork      unitOfWork
		solutionStorage solutionStorage

		messageProducer messageProducer
	}

	unitOfWork interface {
		Do(context.Context, func(ctx context.Context) error) error
	}

	solutionStorage interface {
		GetByExecutionID(context.Context, execution.ID) (testing.Solution, error)
		Update(context.Context, testing.Solution) error
	}

	messageProducer interface {
		Produce(ctx context.Context, msg messages.Message) error
	}
)

func NewUseCase(
	log *slog.Logger,
	solutionStorage solutionStorage,
	unitOfWork unitOfWork,
	messageProducer messageProducer,
) *UseCase {
	return &UseCase{
		log: log,

		unitOfWork:      unitOfWork,
		solutionStorage: solutionStorage,

		messageProducer: messageProducer,
	}
}

func (uc *UseCase) Update(ctx context.Context, command Command) error {
	return uc.unitOfWork.Do(ctx, func(ctx context.Context) error {
		sol, err := uc.solutionStorage.GetByExecutionID(ctx, command.Event.GetExecutionID())
		if err != nil && errors.Is(err, postgres.ErrSolutionByExecutionNotFound) {
			return nil
		}
		if err != nil {
			return fmt.Errorf("failed to get solution by execution id: %w", err)
		}

		msg, hasMsg, err := uc.handleExecutionEvent(&sol, command.Event, time.Now())
		if err != nil {
			return err
		}
		if hasMsg {
			if err = uc.messageProducer.Produce(ctx, msg); err != nil {
				return fmt.Errorf("failed to produce message: %w", err)
			}
		}

		if err = uc.solutionStorage.Update(ctx, sol); err != nil {
			return fmt.Errorf("failed to update solution in storage: %w", err)
		}

		return nil
	})
}

func (uc *UseCase) handleExecutionEvent(sol *testing.Solution, evt events.Event, now time.Time) (messages.Message, bool, error) {
	if sol.FinishedAt != nil {
		return messages.Message{}, false, nil
	}

	switch evt.GetType() {
	case event.StartExecution:
		if sol.StartedAt != nil {
			sol.StartedAt = &now
		}
		return messages.NewStartTestingMessage(sol.ExternalID), true, nil
	case event.FinishExecution:
		typedEvent := evt.AsFinishExecutionEvent()

		sol.FinishedAt = &now
		verdict := sol.TestingStrategy.GetVerdict()

		if typedEvent.Error != nil {
			return messages.NewFinishTestingMessageWithError(sol.ExternalID, verdict, *typedEvent.Error), true, nil
		}
		return messages.NewFinishTestingMessage(sol.ExternalID, verdict), true, nil
	default:
		jobName, jobStatus, err := uc.getJobNameAndStatus(evt)
		if err != nil {
			return messages.Message{}, false, err
		}

		sol.TestingStrategy.UpdateJobStatus(jobName, jobStatus)

		testingStatus := sol.TestingStrategy.GetTestingStatus()
		if sol.LastTestingStatus != nil && testingStatus == *sol.LastTestingStatus {
			return messages.Message{}, false, nil
		}
		sol.LastTestingStatus = &testingStatus
		return messages.NewUpdateStatusMessage(sol.ExternalID, testingStatus), true, nil
	}
}

func (uc *UseCase) getJobNameAndStatus(evt events.Event) (job.Name, job.Status, error) {
	switch evt.GetType() {
	case event.CompileJob:
		typedEvt := evt.AsCompileJobEvent()
		return typedEvt.JobName, typedEvt.CompileStatus, nil
	case event.RunJob:
		typedEvt := evt.AsRunJobEvent()
		return typedEvt.JobName, typedEvt.RunStatus, nil
	case event.CheckJob:
		typedEvt := evt.AsCheckJobEvent()
		return typedEvt.JobName, typedEvt.CheckStatus, nil
	default:
		return "", "", fmt.Errorf("unknown event type: %s", evt.GetType())
	}
}
