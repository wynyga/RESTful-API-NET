using System.ComponentModel.DataAnnotations;
using Business;
using Business.Services;
using Microsoft.Extensions.Configuration;
using Models;
using Models.DTOs;

namespace Tests
{
    public class ProjectStatusRulesTests
    {
        private static readonly DateTime Today = new(2026, 10, 8);

        [Fact]
        public void CompletedStaysCompletedEvenWhenTheEndDateHasPassed()
        {
            Assert.Equal(ProjectStatus.Completed,
                ProjectStatusRules.Effective(ProjectStatus.Completed, Today.AddDays(-30), Today));
        }

        [Fact]
        public void UnfinishedProjectIsOverdueOnceTheEndDateHasPassed()
        {
            Assert.Equal(ProjectStatus.Overdue,
                ProjectStatusRules.Effective(ProjectStatus.OnProgress, Today.AddDays(-1), Today));
        }

        [Fact]
        public void ProjectEndingTodayIsStillOnProgress()
        {
            Assert.Equal(ProjectStatus.OnProgress,
                ProjectStatusRules.Effective(ProjectStatus.OnProgress, Today, Today));
        }

        [Fact]
        public void LegacyStoredOverdueIsRecomputedFromTheEndDate()
        {
            // Rows written by the old API may hold "Overdue"; the end date decides now.
            Assert.Equal(ProjectStatus.OnProgress,
                ProjectStatusRules.Effective(ProjectStatus.Overdue, Today.AddDays(5), Today));
            Assert.Equal(ProjectStatus.Overdue,
                ProjectStatusRules.Effective(ProjectStatus.Overdue, Today.AddDays(-5), Today));
        }

        [Theory]
        [InlineData(ProjectStatus.Completed, ProjectStatus.Completed)]
        [InlineData(ProjectStatus.OnProgress, ProjectStatus.OnProgress)]
        [InlineData(ProjectStatus.Overdue, ProjectStatus.OnProgress)]
        public void OnlyCompletedAndOnProgressArePersisted(string requested, string stored)
        {
            Assert.Equal(stored, ProjectStatusRules.ToStored(requested));
        }
    }

    public class EncryptionServiceTests
    {
        private static EncryptionService Create(string? salt) =>
            new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["HASHIDS_SALT"] = salt }).Build());

        [Fact]
        public void IdsRoundTripThroughTheirObfuscatedForm()
        {
            var service = Create("a-test-salt");
            var token = service.EncryptId(42);

            Assert.True(token.Length >= 8);
            Assert.DoesNotContain("42", token);
            Assert.Equal(42, service.DecryptId(token));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-a-valid-id!!")]
        [InlineData("zzzzzzzzzz")]
        public void GarbageIsRejectedInsteadOfThrowing(string garbage)
        {
            var service = Create("a-test-salt");

            Assert.False(service.TryDecryptId(garbage, out _));
            Assert.Throws<ArgumentException>(() => service.DecryptId(garbage));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("short")]
        public void ASaltThatIsMissingOrTooShortStopsStartup(string? salt)
        {
            Assert.Throws<InvalidOperationException>(() => Create(salt));
        }
    }

    public class ProjectRequestValidationTests
    {
        private static List<ValidationResult> Validate(ProjectRequestDTO dto)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
            return results;
        }

        private static ProjectRequestDTO Valid() => new()
        {
            ProjectName = "Proyek",
            Status = ProjectStatus.OnProgress,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 6, 1),
            Progress = 50,
        };

        [Fact]
        public void AValidRequestPasses() => Assert.Empty(Validate(Valid()));

        [Fact]
        public void EndDateBeforeStartDateIsRejected()
        {
            var dto = Valid();
            dto.EndDate = dto.StartDate.AddDays(-1);
            Assert.Contains(Validate(dto), r => r.MemberNames.Contains(nameof(ProjectRequestDTO.EndDate)));
        }

        [Fact]
        public void CompletedRequiresFullProgress()
        {
            var dto = Valid();
            dto.Status = ProjectStatus.Completed;
            dto.Progress = 50;
            Assert.Contains(Validate(dto), r => r.MemberNames.Contains(nameof(ProjectRequestDTO.Progress)));

            dto.Progress = 100;
            Assert.Empty(Validate(dto));
        }

        [Fact]
        public void FullProgressRequiresCompleted()
        {
            var dto = Valid();
            dto.Progress = 100;
            Assert.Contains(Validate(dto), r => r.MemberNames.Contains(nameof(ProjectRequestDTO.Status)));
        }

        [Theory]
        [InlineData("Done")]
        [InlineData("on progress")]
        public void UnknownStatusIsRejected(string status)
        {
            var dto = Valid();
            dto.Status = status;
            Assert.NotEmpty(Validate(dto));
        }
    }
}
