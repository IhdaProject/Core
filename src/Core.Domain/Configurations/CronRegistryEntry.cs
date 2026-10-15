using NCrontab;

namespace Core.Domain.Configurations;

public sealed record CronRegistryEntry(Type Type, CrontabSchedule CrontabSchedule);
