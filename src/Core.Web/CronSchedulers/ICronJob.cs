namespace Core.Web.CronSchedulers;

public interface ICronJob
{
    Task Run(CancellationToken token = default);
}
