using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace IdenityNotAsyncRepro
{
    public class UserManagerAsyncTest : DbConnectionInterceptor, IDisposable, IAsyncLifetime
    {
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        {
            // Using MSI Authentication, no synchronous version of this provided
            // sqlConnection.AccessToken = Task.Run(() => tokenProvider.GetAccessTokenAsync("https://database.windows.net/")).Result;
            connectionOpened = true;
            connectionOpenedSync = true;
            return result;
        }

        public override async Task<InterceptionResult> ConnectionOpeningAsync(
           DbConnection connection,
           ConnectionEventData eventData,
           InterceptionResult result,
           CancellationToken cancellationToken = default)
        {
            // Using MSI Authentication
            //sqlConnection.AccessToken = await tokenProvider.GetAccessTokenAsync("https://database.windows.net/");
            connectionOpened = true;
            connectionOpenedAsync = true;
            return result;
        }


        public UserManagerAsyncTest()
        {
            var services = new ServiceCollection();
            services
                .AddDbContext<IdentityDbContext>(o => o.UseSqlite($"Data Source={Guid.NewGuid()}.db")
                .AddInterceptors(this));
            services
                .AddIdentityCore<IdentityUser>(opts=> opts.User.RequireUniqueEmail = true)
                .AddEntityFrameworkStores<IdentityDbContext>();

            var sp = services.BuildServiceProvider();
            scope = sp.CreateScope();
            user = new IdentityUser { UserName = "Andy", Email = "andy@somehwere" };            
        }

        public async Task InitializeAsync()
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
            userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            await userManager.CreateAsync(user, "1DigitSeveralLetters!");
            connectionOpened = false;
            connectionOpenedSync = false;
            connectionOpenedAsync = false;
        }

        [Fact]
        public async Task CreateAsync()
        {
            var user = new IdentityUser { UserName = "Bob" , Email= "bob@somehwere" };
            var result = await userManager.CreateAsync(user, "1DigitSeveralLetters!");
            Assert.True(result.Succeeded, "User should be created");
            Assert.True(connectionOpened, "A database connection should have been opened");
            Assert.True(connectionOpenedAsync, "The database connection should be opened asynchonously");
            Assert.False(connectionOpenedSync, "The database connection should not be opened synchonously");
        }

        [Fact]
        public async Task FindByEmailAsync()
        {
            var result = await userManager.FindByEmailAsync("andy@somehwere");
            Assert.NotNull(result);
            Assert.True(connectionOpened, "A database connection should have been opened");
            Assert.True(connectionOpenedAsync, "The database connection should be opened asynchonously");
            Assert.False(connectionOpenedSync, "The database connection should not be opened synchonously");
        }

        [Fact]
        public async Task UpdateAsync()
        {
            user.PhoneNumber = "anumber";
            var result = await userManager.UpdateAsync(user);
            Assert.True(result.Succeeded);
            Assert.True(connectionOpened, "A database connection should have been opened");
            Assert.True(connectionOpenedAsync, "The database connection should be opened asynchonously");
            Assert.False(connectionOpenedSync, "The database connection should not be opened synchonously");
        }

        public async Task DisposeAsync()
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.EnsureDeletedAsync();
        }

        public void Dispose()
        {
            scope.Dispose();
        }


        private volatile bool connectionOpened = false;
        private volatile bool connectionOpenedAsync = false;
        private volatile bool connectionOpenedSync = false;
        private volatile UserManager<IdentityUser> userManager;

        private readonly IServiceScope scope;
        private IdentityUser user;
    }
}
