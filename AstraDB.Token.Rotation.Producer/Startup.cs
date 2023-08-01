using AstraDB.Token.Rotation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace AstraDB.Token.Rotation.Producer
{
    public static class Startup
    {
        public async static Task ConfigureServices()
        {
            var builder = Host.CreateDefaultBuilder()
                .ConfigureServices((cxt, services) =>
                {
                    services.AddQuartz(q =>
                    {
                        q.UseMicrosoftDependencyInjectionJobFactory();
                    });
                    services.AddQuartzHostedService(opt =>
                    {
                        opt.WaitForJobsToComplete = true;
                    });

                    services.AddTransient<IConfigurationService, IConfigurationService>();
                    services.AddTransient<IKeyVaultService, KeyVaultService>();
                    services.AddTransient<ITokenRotationService, TokenRotationService>();
                    services.AddTransient<IConfluentService, ConfluentService>();
                }).Build();

            var schedulerFactory = builder.Services.GetRequiredService<ISchedulerFactory>();
            var scheduler = await schedulerFactory.GetScheduler();

            // define the job and tie it to our TokenRotationJob class
            var job = JobBuilder.Create<TokenRotationJob>()
                .WithIdentity("TokenRotationJob", "TokenRotationGroup")
                .Build();

            // Trigger the job to run now, and then every 40 seconds
            var trigger = TriggerBuilder.Create()
                .WithIdentity("TokenRotationTrigger", "TokenRotationGroup")
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(10)
                    .RepeatForever())
                .Build();

            await scheduler.ScheduleJob(job, trigger);

            // will block until the last running job completes
            await builder.RunAsync();
        }
    }
}