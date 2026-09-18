using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RentalApp.Application.Applications;
using RentalApp.Application.Properties;
using RentalApp.Application.Units;
using RentalApp.Domain.Common;
using RentalApp.Domain.Entities;
using RentalApp.Domain.Enums;
using RentalApp.Infrastructure.Identity;
using RentalApp.UnitTests.Support;
using RentalApp.Web.Controllers;

namespace RentalApp.UnitTests.Web;

// Real MVC binding, antiforgery and compiled Razor rendering, with in-memory service/repository
// doubles. These regression tests do not replace the SQL Server integration tests.
public sealed class ModalWorkflowTests
{
    [Theory]
    [InlineData("CreateModal")]
    [InlineData("EditModal")]
    [InlineData("Delete")]
    public async Task PropertySuccess_ReturnsAndRendersOnlyListFragment(string action)
    {
        await using var app = await MvcApp.StartAsync(manager: true);
        var html = await app.Get("/Properties/Index");
        var fields = new Dictionary<string, string>
        {
            ["Id"] = "1", ["Name"] = "Updated property", ["AddressLine1"] = "1 Main St",
            ["City"] = "City", ["State"] = "ST", ["PostalCode"] = "10001"
        };
        var response = await app.Post("/Properties/" + action, html, fields);
        var fragment = await app.Refresh(response, "#property-list");
        Assert.DoesNotContain("<html", fragment);
        Assert.DoesNotContain("<nav", fragment);
        Assert.Contains(action == "Delete" ? "No properties yet" : "Updated property", fragment);
    }

    [Fact]
    public async Task InvalidProperty_ReturnsSameModalWithErrors()
    {
        await using var app = await MvcApp.StartAsync(manager: true);
        var html = await app.Get("/Properties/CreateModal");
        var response = await app.Post("/Properties/CreateModal", html, new() { ["Name"] = "" });
        var invalid = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-modal-form", invalid);
        Assert.Contains("The Name field is required", invalid);
        Assert.DoesNotContain("<html", invalid);
        Assert.Single(app.Properties.Items);
    }

    [Theory]
    [InlineData(ReviewOutcome.Approve, ApplicationStatus.Approved)]
    [InlineData(ReviewOutcome.Return, ApplicationStatus.Returned)]
    [InlineData(ReviewOutcome.Deny, ApplicationStatus.Denied)]
    public async Task ReviewSuccess_RefreshesStatusActionsAndHistory(ReviewOutcome outcome, ApplicationStatus status)
    {
        await using var app = await MvcApp.StartAsync(manager: true);
        app.Repository.Application.Status = ApplicationStatus.UnderReview;
        app.Repository.Application.ClaimedByUserId = "manager";
        var html = await app.Get("/Applications/ReviewModal?id=1");
        var response = await app.Post("/Applications/ReviewModal", html, new()
        {
            ["ApplicationId"] = "1", ["Outcome"] = outcome.ToString(), ["Comment"] = "Decision comment"
        });
        var fragment = await app.Refresh(response, "#application-detail, [data-application-grid]");
        Assert.Equal(status, app.Repository.Application.Status);
        Assert.Contains(status.ToString(), fragment);
        Assert.DoesNotContain(">Review</button>", fragment);
        Assert.DoesNotContain("<html", fragment);
        Assert.Equal(outcome == ReviewOutcome.Approve ? 1 : 0, app.Repository.Application.Unit.Leases.Count);
        Assert.Contains(app.Repository.History, h => h.ToStatus == status && h.Comment == "Decision comment");
    }

    [Theory]
    [InlineData(ReviewOutcome.Return)]
    [InlineData(ReviewOutcome.Deny)]
    public async Task InvalidReview_StaysInModalAndDoesNotChangeStatus(ReviewOutcome outcome)
    {
        await using var app = await MvcApp.StartAsync(manager: true);
        app.Repository.Application.Status = ApplicationStatus.UnderReview;
        app.Repository.Application.ClaimedByUserId = "manager";
        var html = await app.Get("/Applications/ReviewModal?id=1");
        var response = await app.Post("/Applications/ReviewModal", html, new()
        {
            ["ApplicationId"] = "1", ["Outcome"] = outcome.ToString(), ["Comment"] = ""
        });
        var invalid = await response.Content.ReadAsStringAsync();
        Assert.Contains("Comment is required", invalid);
        Assert.Contains("data-modal-form", invalid);
        Assert.Equal(ApplicationStatus.UnderReview, app.Repository.Application.Status);
    }

    [Fact]
    public async Task Summary_RenderedFormsAreSiblings_AndWizardOwnsSubmitAndBack()
    {
        await using var app = await MvcApp.StartAsync();
        var html = await app.Get("/Applications/Wizard?id=1&section=Summary");
        var depth = 0;
        foreach (Match tag in Regex.Matches(html, @"</?form\b[^>]*>", RegexOptions.IgnoreCase))
        {
            depth += tag.Value.StartsWith("</") ? -1 : 1;
            Assert.InRange(depth, 0, 1);
        }
        Assert.Equal(0, depth);
        var forms = Regex.Matches(html, @"<form\b[^>]*>.*?</form>", RegexOptions.Singleline);
        var wizard = Assert.Single(forms.Cast<Match>(), m => m.Value.Contains("id=\"application-wizard\"")).Value;
        Assert.Contains("action=\"/Applications/Wizard\"", wizard);
        Assert.Contains("value=\"Submit\"", wizard);
        Assert.Contains("value=\"Back\"", wizard);
        Assert.DoesNotContain("AddCoApplicant", wizard);
        Assert.DoesNotContain("RemoveCoApplicant", wizard);
        foreach (var action in new[] { "AddCoApplicant", "RemoveCoApplicant" })
        {
            var form = Assert.Single(forms.Cast<Match>(), m => m.Value.Contains("/Applications/" + action)).Value;
            Assert.Contains("__RequestVerificationToken", form);
        }
        var response = await app.Post("/Applications/Wizard", html, new()
        {
            ["Id"] = "1", ["DisplaySection"] = "Summary", ["Action"] = "Submit"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(ApplicationStatus.Submitted, app.Repository.Application.Status);
    }

    [Fact]
    public async Task ApplicantInvalidSave_RendersNewVersion_RetrySucceeds_StaleWriteStillFails()
    {
        await using var app = await MvcApp.StartAsync();
        var html = await app.Get("/Applications/Wizard?id=1&section=ApplicantInfo");
        var fields = new Dictionary<string, string>
        {
            ["Id"] = "1", ["DisplaySection"] = "ApplicantInfo", ["Action"] = "Continue",
            ["ApplicantInfoVersion"] = Input(html, "ApplicantInfoVersion"), ["FullName"] = "",
            ["Phone"] = "555", ["Email"] = "owner@example.test", ["CurrentAddress"] = "Current address"
        };
        var response = await app.Post("/Applications/Wizard", html, fields);
        var invalid = await response.Content.ReadAsStringAsync();
        Assert.Contains("Full name is required", invalid);
        Assert.Equal("1", Input(invalid, "ApplicantInfoVersion"));
        fields["ApplicantInfoVersion"] = Input(invalid, "ApplicantInfoVersion");
        fields["FullName"] = "Corrected applicant";
        response = await app.Post("/Applications/Wizard", invalid, fields);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(2, app.Repository.Application.ApplicantInfoVersion);
        // Reusing the successful request's old token must not silently overwrite.
        response = await app.Post("/Applications/Wizard", invalid, fields);
        Assert.Contains("Reload the latest version", await response.Content.ReadAsStringAsync());
        Assert.Equal(2, app.Repository.Application.ApplicantInfoVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResidenceInvalidAddOrEdit_RendersCurrentIdAndToken_CorrectionAndDeleteRefreshFragment(bool edit)
    {
        await using var app = await MvcApp.StartAsync();
        var html = await app.Get("/Applications/ResidenceModal?applicationId=1" + (edit ? "&id=1" : ""));
        var fields = new Dictionary<string, string>
        {
            ["ApplicationId"] = "1", ["Id"] = Input(html, "Id"), ["Address"] = "",
            ["LandlordName"] = "Landlord", ["LandlordPhone"] = "555", ["MoveInDate"] = "2020-01-01",
            ["ExpectedResidenceHistoryVersion"] = Input(html, "ExpectedResidenceHistoryVersion")
        };
        var response = await app.Post("/Applications/ResidenceModal", html, fields);
        var invalid = await response.Content.ReadAsStringAsync();
        Assert.Contains("Address is required", invalid);
        Assert.Equal("1", Input(invalid, "ExpectedResidenceHistoryVersion"));
        Assert.Equal(edit ? "1" : "2", Input(invalid, "Id"));
        fields["Id"] = Input(invalid, "Id");
        fields["ExpectedResidenceHistoryVersion"] = Input(invalid, "ExpectedResidenceHistoryVersion");
        fields["Address"] = "Corrected address";
        response = await app.Post("/Applications/ResidenceModal", invalid, fields);
        var fragment = await app.Refresh(response, "#application-detail");
        Assert.Contains("Corrected address", fragment);
        Assert.Equal("2", Input(fragment, "ResidenceHistoryVersion"));
        Assert.Equal(edit ? 1 : 2, app.Repository.Application.Residences.Count);
        response = await app.Post("/Applications/ResidenceModal", invalid, fields);
        Assert.Contains("Reload the latest version", await response.Content.ReadAsStringAsync());
        Assert.Equal(2, app.Repository.Application.ResidenceHistoryVersion);
        var deleteHtml = await app.Get("/Applications/DeleteResidenceModal?applicationId=1&id=" + fields["Id"]);
        response = await app.Post("/Applications/DeleteResidence", deleteHtml, new()
        {
            ["ApplicationId"] = "1", ["Id"] = fields["Id"],
            ["ExpectedResidenceHistoryVersion"] = Input(deleteHtml, "ExpectedResidenceHistoryVersion")
        });
        fragment = await app.Refresh(response, "#application-detail");
        Assert.Equal("3", Input(fragment, "ResidenceHistoryVersion"));
        Assert.DoesNotContain("Corrected address", fragment);
    }

    [Fact]
    public async Task ResidenceContinue_InvalidPersistedSave_ReturnsCurrentVersionAndFieldErrors()
    {
        await using var app = await MvcApp.StartAsync();
        app.Repository.Application.Residences.Single().Address = null;
        app.Repository.Application.ResidenceHistorySaved = true;
        var html = await app.Get("/Applications/Wizard?id=1&section=ResidenceHistory");
        var fields = new Dictionary<string, string>
        {
            ["Id"] = "1", ["DisplaySection"] = "ResidenceHistory", ["Action"] = "Continue",
            ["ResidenceHistoryVersion"] = Input(html, "ResidenceHistoryVersion")
        };
        var response = await app.Post("/Applications/Wizard", html, fields);
        var invalid = await response.Content.ReadAsStringAsync();
        Assert.Contains("Address is required", invalid);
        Assert.Equal("1", Input(invalid, "ResidenceHistoryVersion"));
        // Correct persisted residence data; the following Continue must use the refreshed token.
        app.Repository.Application.Residences.Single().Address = "Corrected address";
        fields["ResidenceHistoryVersion"] = Input(invalid, "ResidenceHistoryVersion");
        response = await app.Post("/Applications/Wizard", invalid, fields);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(2, app.Repository.Application.ResidenceHistoryVersion);
    }

    [Fact]
    public async Task FragmentEndpoints_KeepAuthorizationAndPostsKeepAntiforgery()
    {
        await using var app = await MvcApp.StartAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Client.GetAsync("/Properties/List")).StatusCode);
        var post = await app.Client.PostAsync("/Applications/RemoveCoApplicant", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["id"] = "1", ["userId"] = "other" }));
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
        app.Client.DefaultRequestHeaders.Remove("X-Test-User");
        app.Client.DefaultRequestHeaders.Add("X-Test-User", "foreign");
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/Applications/WizardFragment?id=1")).StatusCode);
    }

    private static string Input(string html, string name)
    {
        var input = Regex.Matches(html, @"<input\b[^>]*>").Cast<Match>()
            .First(m => m.Value.Contains("name=\"" + name + "\""));
        return WebUtility.HtmlDecode(Regex.Match(input.Value, "value=\"([^\"]*)\"").Groups[1].Value);
    }

    private sealed class MvcApp : IAsyncDisposable
    {
        private readonly WebApplication _host;
        public HttpClient Client { get; }
        public ApplicationRepositoryFake Repository { get; }
        public PropertyServiceStub Properties { get; }

        private MvcApp(WebApplication host, ApplicationRepositoryFake repository, PropertyServiceStub properties, bool manager)
        {
            _host = host;
            Repository = repository;
            Properties = properties;
            Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
            { BaseAddress = new Uri(host.Urls.Single()) };
            Client.DefaultRequestHeaders.Add("X-Test-User", manager ? "manager" : "owner");
        }

        public static async Task<MvcApp> StartAsync(bool manager = false)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "RentalApp.sln"))) root = root.Parent;
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(ApplicationsController).Assembly.GetName().Name,
                ContentRootPath = Path.Combine(root!.FullName, "src", "RentalApp.Web"), EnvironmentName = "Development"
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseKestrel(o => o.Listen(IPAddress.Loopback, 0, listen => listen.Protocols = HttpProtocols.Http1));
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ApplicationsController).Assembly);
            builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("test", _ => { });
            builder.Services.AddAuthorization();
            var repository = new ApplicationRepositoryFake();
            repository.Application.Status = ApplicationStatus.Draft;
            repository.Application.Applicants.Add(new ApplicationApplicant { RentalApplicationId = 1, UserId = "other" });
            repository.Application.Residences.Add(new ResidenceHistory
            {
                Id = 1, RentalApplicationId = 1, Address = "Original residence", LandlordName = "Landlord",
                LandlordPhone = "555", MoveInDate = new DateOnly(2020, 1, 1)
            });
            var properties = new PropertyServiceStub();
            builder.Services.AddSingleton<IRentalApplicationRepository>(repository);
            builder.Services.AddScoped<IApplicationCommandService, ApplicationCommandService>();
            builder.Services.AddScoped<IApplicationQueryService, ApplicationQueryService>();
            builder.Services.AddSingleton<IPropertyService>(properties);
            builder.Services.AddSingleton<IUnitService, UnitServiceStub>();
            builder.Services.AddSingleton<IPropertyManagerNoteRepository, PropertyManagerNoteRepositoryFake>();
            builder.Services.AddScoped<IPropertyManagerNoteService, PropertyManagerNoteService>();
            builder.Services.AddIdentityCore<ApplicationUser>().AddUserStore<UserStoreStub>();
            var host = builder.Build();
            host.UseRouting();
            host.UseAuthentication();
            host.UseAuthorization();
            host.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            await host.StartAsync();
            return new MvcApp(host, repository, properties, manager);
        }

        public async Task<string> Get(string path)
        {
            var response = await Client.GetAsync(path);
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"GET {path}: {response.StatusCode}\n{body}");
            return body;
        }

        public Task<HttpResponseMessage> Post(string path, string html, Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = Input(html, "__RequestVerificationToken");
            return Client.PostAsync(path, new FormUrlEncodedContent(fields));
        }

        public async Task<string> Refresh(HttpResponseMessage response, string target)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(target, json.RootElement.GetProperty("refreshTarget").GetString());
            return await Get(json.RootElement.GetProperty("refreshUrl").GetString()!);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _host.StopAsync();
            await _host.DisposeAsync();
        }
    }

    private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var user = Request.Headers["X-Test-User"].ToString();
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user), new Claim(ClaimTypes.Name, user),
                new Claim(ClaimTypes.Role, user == "manager" ? AppRoles.PropertyManager : AppRoles.Applicant) };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), "test")));
        }
    }

    private sealed class PropertyServiceStub : IPropertyService
    {
        public List<PropertyDto> Items { get; } = [new(1, "Original property", "1 Main St", "City", "ST", "10001", 0)];
        public Task<IReadOnlyList<PropertyDto>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<PropertyDto>>(Items);
        public Task<PropertyDto?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(p => p.Id == id));
        public Task<Result<int>> CreateAsync(CreatePropertyCommand c, CancellationToken ct = default)
        { Items.Add(new(2, c.Name, c.AddressLine1, c.City, c.State, c.PostalCode, 0)); return Task.FromResult(Result.Success(2)); }
        public Task<Result> UpdateAsync(UpdatePropertyCommand c, CancellationToken ct = default)
        { Items[0] = new(c.Id, c.Name, c.AddressLine1, c.City, c.State, c.PostalCode, 0); return Task.FromResult(Result.Success()); }
        public Task<Result> DeleteAsync(int id, CancellationToken ct = default)
        { Items.RemoveAll(p => p.Id == id); return Task.FromResult(Result.Success()); }
    }

    private sealed class UnitServiceStub : IUnitService
    {
        public Task<IReadOnlyList<UnitDto>> GetByPropertyAsync(int id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<UnitDto>>([]);
        public Task<IReadOnlyList<UnitDto>> GetAvailableAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UnitDto?> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<int>> CreateAsync(CreateUnitCommand c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result> UpdateAsync(UpdateUnitCommand c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result> DeleteAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UnitTypeDto>> GetUnitTypesForSelectAsync(int? id = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class UserStoreStub : IUserStore<ApplicationUser>
    {
        public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken ct) => Task.FromResult<ApplicationUser?>(new() { Id = id, FullName = id });
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.Id);
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.UserName);
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.NormalizedUserName);
        public Task SetUserNameAsync(ApplicationUser user, string? name, CancellationToken ct) { user.UserName = name; return Task.CompletedTask; }
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? name, CancellationToken ct) { user.NormalizedUserName = name; return Task.CompletedTask; }
        public Task<ApplicationUser?> FindByNameAsync(string name, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken ct) => throw new NotSupportedException();
        public void Dispose() { }
    }
}
