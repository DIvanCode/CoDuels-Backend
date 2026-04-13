using Duely.Domain.Models.UserActions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duely.Infrastructure.DataAccess.EntityFramework.Configurations;

public sealed class UserActionsConfiguration : IEntityTypeConfiguration<UserAction>
{
    private const string DiscriminatorProperty = "UserActionDiscriminator";

    public void Configure(EntityTypeBuilder<UserAction> builder)
    {
        builder.ToTable("UserActions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName(nameof(UserAction.Id))
            .ValueGeneratedOnAdd()
            .UseIdentityByDefaultColumn();

        builder.Property(e => e.EventId)
            .HasColumnName(nameof(UserAction.EventId))
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.SequenceId)
            .HasColumnName(nameof(UserAction.SequenceId))
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.Timestamp)
            .HasColumnName(nameof(UserAction.Timestamp))
            .HasColumnType("timestamp")
            .IsRequired();

        builder.Property(e => e.DuelId)
            .HasColumnName(nameof(UserAction.DuelId))
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.TaskKey)
            .HasColumnName(nameof(UserAction.TaskKey))
            .HasColumnType("varchar(1)")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName(nameof(UserAction.UserId))
            .HasColumnType("integer")
            .IsRequired();

        builder.HasDiscriminator<UserActionType>(DiscriminatorProperty)
            .HasValue<ChooseLanguageUserAction>(UserActionType.ChooseLanguage)
            .HasValue<WriteCodeUserAction>(UserActionType.WriteCode)
            .HasValue<DeleteCodeUserAction>(UserActionType.DeleteCode)
            .HasValue<PasteCodeUserAction>(UserActionType.PasteCode)
            .HasValue<CutCodeUserAction>(UserActionType.CutCode)
            .HasValue<MoveCursorUserAction>(UserActionType.MoveCursor)
            .HasValue<RunSampleTestUserAction>(UserActionType.RunSampleTest)
            .HasValue<RunCustomTestUserAction>(UserActionType.RunCustomTest)
            .HasValue<SubmitSolutionUserAction>(UserActionType.SubmitSolution);

        builder.Property<UserActionType>(DiscriminatorProperty)
            .HasColumnName("Type")
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired();

        builder.UseTphMappingStrategy();
    }
}

public sealed class ChooseLanguageUserActionsConfiguration : IEntityTypeConfiguration<ChooseLanguageUserAction>
{
    public void Configure(EntityTypeBuilder<ChooseLanguageUserAction> builder)
    {
        builder.Property(e => e.Language)
            .HasColumnName(nameof(ChooseLanguageUserAction.Language))
            .HasColumnType("text")
            .HasConversion<string>()
            .IsRequired();
    }
}

public sealed class WriteCodeUserActionsConfiguration : IEntityTypeConfiguration<WriteCodeUserAction>
{
    public void Configure(EntityTypeBuilder<WriteCodeUserAction> builder)
    {
        builder.Property(e => e.CodeLength)
            .HasColumnName(nameof(WriteCodeUserAction.CodeLength))
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.CursorLine)
            .HasColumnName(nameof(WriteCodeUserAction.CursorLine))
            .HasColumnType("integer")
            .IsRequired();
    }
}

public sealed class DeleteCodeUserActionsConfiguration : IEntityTypeConfiguration<DeleteCodeUserAction>
{
    public void Configure(EntityTypeBuilder<DeleteCodeUserAction> builder)
    {
        builder.Property(e => e.CodeLength)
            .HasColumnName(nameof(DeleteCodeUserAction.CodeLength))
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.CursorLine)
            .HasColumnName(nameof(DeleteCodeUserAction.CursorLine))
            .HasColumnType("integer")
            .IsRequired();
    }
}

public sealed class PasteCodeUserActionsConfiguration : IEntityTypeConfiguration<PasteCodeUserAction>
{
    public void Configure(EntityTypeBuilder<PasteCodeUserAction> builder)
    {
        builder.Property(e => e.CodeLength)
            .HasColumnName(nameof(PasteCodeUserAction.CodeLength))
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.CursorLine)
            .HasColumnName(nameof(PasteCodeUserAction.CursorLine))
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.BeginLine)
            .HasColumnName(nameof(PasteCodeUserAction.BeginLine))
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.EndLine)
            .HasColumnName(nameof(PasteCodeUserAction.EndLine))
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.CharsCount)
            .HasColumnName(nameof(PasteCodeUserAction.CharsCount))
            .HasColumnType("integer")
            .IsRequired();
    }
}

public sealed class CutCodeUserActionsConfiguration : IEntityTypeConfiguration<CutCodeUserAction>
{
    public void Configure(EntityTypeBuilder<CutCodeUserAction> builder)
    {
        builder.Property(e => e.CodeLength)
            .HasColumnName(nameof(CutCodeUserAction.CodeLength))
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.CursorLine)
            .HasColumnName(nameof(CutCodeUserAction.CursorLine))
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.BeginLine)
            .HasColumnName(nameof(CutCodeUserAction.BeginLine))
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.EndLine)
            .HasColumnName(nameof(CutCodeUserAction.EndLine))
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.CharsCount)
            .HasColumnName(nameof(CutCodeUserAction.CharsCount))
            .HasColumnType("integer")
            .IsRequired();
    }
}

public sealed class MoveCursorUserActionsConfiguration : IEntityTypeConfiguration<MoveCursorUserAction>
{
    public void Configure(EntityTypeBuilder<MoveCursorUserAction> builder)
    {
        builder.Property(e => e.CodeLength)
            .HasColumnName(nameof(MoveCursorUserAction.CodeLength))
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.CursorLine)
            .HasColumnName(nameof(MoveCursorUserAction.CursorLine))
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.PreviousCursorLine)
            .HasColumnName(nameof(MoveCursorUserAction.PreviousCursorLine))
            .HasColumnType("integer")
            .IsRequired();
    }
}

public sealed class RunSampleTestUserActionsConfiguration : IEntityTypeConfiguration<RunSampleTestUserAction>
{
    public void Configure(EntityTypeBuilder<RunSampleTestUserAction> builder)
    {
    }
}

public sealed class RunCustomTestUserActionsConfiguration : IEntityTypeConfiguration<RunCustomTestUserAction>
{
    public void Configure(EntityTypeBuilder<RunCustomTestUserAction> builder)
    {
    }
}

public sealed class SubmitSolutionUserActionsConfiguration : IEntityTypeConfiguration<SubmitSolutionUserAction>
{
    public void Configure(EntityTypeBuilder<SubmitSolutionUserAction> builder)
    {
    }
}
