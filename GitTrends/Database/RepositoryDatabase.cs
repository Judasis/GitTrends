﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GitTrends.Shared;
using SQLite;
using Xamarin.Forms.Xaml;

namespace GitTrends
{
    public class RepositoryDatabase : BaseDatabase
    {
        public async Task DeleteAllData()
        {
            var (repositoryDatabaseConnection, dailyClonesDatabaseConnection, dailyViewsDatabaseConnection) = await GetDatabaseConnections().ConfigureAwait(false);

            await AttemptAndRetry(() => repositoryDatabaseConnection.DeleteAllAsync<RepositoryDatabaseModel>()).ConfigureAwait(false);
            await AttemptAndRetry(() => dailyViewsDatabaseConnection.DeleteAllAsync<DailyViewsDatabaseModel>()).ConfigureAwait(false);
            await AttemptAndRetry(() => dailyClonesDatabaseConnection.DeleteAllAsync<DailyClonesDatabaseModel>()).ConfigureAwait(false);
        }

        public async Task SaveRepository(Repository repository)
        {
            var databaseConnection = await GetDatabaseConnection<RepositoryDatabaseModel>().ConfigureAwait(false);

            var repositoryDatabaseModel = RepositoryDatabaseModel.ToRepositoryDatabase(repository);
            await AttemptAndRetry(() => databaseConnection.InsertOrReplaceAsync(repositoryDatabaseModel)).ConfigureAwait(false);


            await SaveDailyClones(repository).ConfigureAwait(false);
            await SaveDailyViews(repository).ConfigureAwait(false);
        }

        public async Task<IEnumerable<Repository>> GetRepositories()
        {
            var (repositoryDatabaseConnection, dailyClonesDatabaseConnection, dailyViewsDatabaseConnection) = await GetDatabaseConnections().ConfigureAwait(false);

            var repositoryDatabaseModels = await AttemptAndRetry(() => repositoryDatabaseConnection.Table<RepositoryDatabaseModel>().ToListAsync()).ConfigureAwait(false);
            var dailyClonesDatabaseModels = await AttemptAndRetry(() => dailyClonesDatabaseConnection.Table<DailyClonesDatabaseModel>().ToListAsync()).ConfigureAwait(false);
            var dailyViewsDatabaseModels = await AttemptAndRetry(() => dailyViewsDatabaseConnection.Table<DailyViewsDatabaseModel>().ToListAsync()).ConfigureAwait(false);

            var sortedRecentDailyClonesDatabaseModels = dailyClonesDatabaseModels.OrderByDescending(x => x.DataDownloadedAt).ToList();
            var sortedRecentDailyViewsDatabaseModels = dailyViewsDatabaseModels.OrderByDescending(x => x.DataDownloadedAt).ToList();

            var mostRecentCloneDay = sortedRecentDailyClonesDatabaseModels.Max(x => x.Day);
            var mostRecentViewDay = sortedRecentDailyViewsDatabaseModels.Max(x => x.Day);

            var mostRecentDate = mostRecentCloneDay.CompareTo(mostRecentViewDay) > 0 ? mostRecentCloneDay : mostRecentViewDay;

            var repositoryList = new List<Repository>();
            foreach (var repositoryDatabaseModel in repositoryDatabaseModels)
            {
                var dailyClones = sortedRecentDailyClonesDatabaseModels.Where(x => x.RepositoryUrl == repositoryDatabaseModel.Url && isWithin14Days(x.Day, mostRecentDate)).GroupBy(x => x.Day).Select(x => x.First()).Take(14);
                var dailyViews = sortedRecentDailyViewsDatabaseModels.Where(x => x.RepositoryUrl == repositoryDatabaseModel.Url && isWithin14Days(x.Day, mostRecentDate)).GroupBy(x => x.Day).Select(x => x.First()).Take(14);

                var repository = RepositoryDatabaseModel.ToRepository(repositoryDatabaseModel, dailyClones, dailyViews);
                repositoryList.Add(repository);
            }

            return repositoryList;

            static bool isWithin14Days(DateTimeOffset dataDate, DateTimeOffset mostRecentDate) => dataDate.CompareTo(mostRecentDate.Subtract(TimeSpan.FromDays(13)).ToLocalTime()) >= 0;
        }


        static async Task<(SQLiteAsyncConnection RepositoryDatabaseConnection,
                        SQLiteAsyncConnection DailyClonesDatabaseConnection,
                        SQLiteAsyncConnection DailyViewsDatabaseConnection)> GetDatabaseConnections()
        {
            var repositoryDatabaseConnection = await GetDatabaseConnection<RepositoryDatabaseModel>().ConfigureAwait(false);
            var dailyClonesDatabaseConnection = await GetDatabaseConnection<DailyClonesDatabaseModel>().ConfigureAwait(false);
            var dailyViewsDatabaseConnection = await GetDatabaseConnection<DailyViewsDatabaseModel>().ConfigureAwait(false);

            return (repositoryDatabaseConnection, dailyClonesDatabaseConnection, dailyViewsDatabaseConnection);
        }

        static async Task SaveDailyClones(Repository repository)
        {
            var dailyClonesDatabaseConnection = await GetDatabaseConnection<DailyClonesDatabaseModel>().ConfigureAwait(false);

            foreach (var dailyClonesModel in repository.DailyClonesList)
            {
                var dailyClonesDatabaseModel = DailyClonesDatabaseModel.ToDailyClonesDatabaseModel(dailyClonesModel, repository);
                await AttemptAndRetry(() => dailyClonesDatabaseConnection.InsertOrReplaceAsync(dailyClonesDatabaseModel)).ConfigureAwait(false);
            }
        }

        static async Task SaveDailyViews(Repository repository)
        {
            var dailyViewsDatabaseConnection = await GetDatabaseConnection<DailyViewsDatabaseModel>().ConfigureAwait(false);

            foreach (var dailyViewsModel in repository.DailyViewsList)
            {
                var dailyViewsDatabaseModel = DailyViewsDatabaseModel.ToDailyViewsDatabaseModel(dailyViewsModel, repository);
                await AttemptAndRetry(() => dailyViewsDatabaseConnection.InsertOrReplaceAsync(dailyViewsDatabaseModel)).ConfigureAwait(false);
            }
        }

        class DailyClonesDatabaseModel : IDailyClonesModel
        {
            public DateTime LocalDay => Day.LocalDateTime;

            public DateTimeOffset Day { get; set; }

            public string RepositoryUrl { get; set; } = string.Empty;

            public DateTimeOffset DataDownloadedAt { get; set; } = DateTimeOffset.UtcNow;

            public long TotalClones { get; set; }

            public long TotalUniqueClones { get; set; }

            public static DailyClonesModel ToDailyClonesModel(in DailyClonesDatabaseModel dailyClonesDatabaseModel) =>
                new DailyClonesModel(dailyClonesDatabaseModel.Day, dailyClonesDatabaseModel.TotalClones, dailyClonesDatabaseModel.TotalUniqueClones);

            public static DailyClonesDatabaseModel ToDailyClonesDatabaseModel(in DailyClonesModel dailyClonesModel, in Repository repository)
            {
                return new DailyClonesDatabaseModel
                {
                    DataDownloadedAt = repository.DataDownloadedAt,
                    RepositoryUrl = repository.Url,
                    Day = dailyClonesModel.Day,
                    TotalClones = dailyClonesModel.TotalClones,
                    TotalUniqueClones = dailyClonesModel.TotalUniqueClones
                };
            }
        }

        class DailyViewsDatabaseModel : IDailyViewsModel
        {
            public DateTime LocalDay => Day.LocalDateTime;

            public DateTimeOffset Day { get; set; }

            public string RepositoryUrl { get; set; } = string.Empty;

            public DateTimeOffset DataDownloadedAt { get; set; } = DateTimeOffset.UtcNow;

            public long TotalViews { get; set; }

            public long TotalUniqueViews { get; set; }

            public static DailyViewsModel ToDailyViewsModel(in DailyViewsDatabaseModel dailyViewsDatabaseModel) =>
                new DailyViewsModel(dailyViewsDatabaseModel.Day, dailyViewsDatabaseModel.TotalViews, dailyViewsDatabaseModel.TotalUniqueViews);

            public static DailyViewsDatabaseModel ToDailyViewsDatabaseModel(in DailyViewsModel dailyViewsModel, in Repository repository)
            {
                return new DailyViewsDatabaseModel
                {
                    DataDownloadedAt = repository.DataDownloadedAt,
                    RepositoryUrl = repository.Url,
                    Day = dailyViewsModel.Day,
                    TotalViews = dailyViewsModel.TotalViews,
                    TotalUniqueViews = dailyViewsModel.TotalUniqueViews
                };
            }
        }

        class RepositoryDatabaseModel : IRepository
        {
            public DateTimeOffset DataDownloadedAt { get; set; } = DateTimeOffset.UtcNow;

            public string Name { get; set; } = string.Empty;

            public string Description { get; set; } = string.Empty;

            public long ForkCount { get; set; }

            [PrimaryKey]
            public string Url { get; set; } = string.Empty;

            public long StarCount { get; set; }

            public string OwnerLogin { get; set; } = string.Empty;

            public string OwnerAvatarUrl { get; set; } = string.Empty;

            public long IssuesCount { get; set; }

            public bool IsFork { get; set; }

            public long TotalViews { get; set; }

            public long TotalUniqueViews { get; set; }

            public long TotalClones { get; set; }

            public long TotalUniqueClones { get; set; }

            public static Repository ToRepository(in RepositoryDatabaseModel repositoryDatabaseModel,
                                                    in IEnumerable<DailyClonesDatabaseModel> dailyClonesDatabaseModels,
                                                    in IEnumerable<DailyViewsDatabaseModel> dailyViewsDatabaseModels)
            {
                var clonesList = dailyClonesDatabaseModels.Select(x => DailyClonesDatabaseModel.ToDailyClonesModel(x)).ToList();
                var viewsList = dailyViewsDatabaseModels.Select(x => DailyViewsDatabaseModel.ToDailyViewsModel(x)).ToList();

                return new Repository(repositoryDatabaseModel.Name,
                                        repositoryDatabaseModel.Description,
                                        repositoryDatabaseModel.ForkCount,
                                        new RepositoryOwner(repositoryDatabaseModel.OwnerLogin, repositoryDatabaseModel.OwnerAvatarUrl ?? repositoryDatabaseModel.OwnerLogin),
                                        new IssuesConnection(repositoryDatabaseModel.IssuesCount, Enumerable.Empty<Issue>()),
                                        repositoryDatabaseModel.Url,
                                        new StarGazers(repositoryDatabaseModel.StarCount),
                                        repositoryDatabaseModel.IsFork,
                                        repositoryDatabaseModel.DataDownloadedAt,
                                        viewsList,
                                        clonesList);
            }

            public static RepositoryDatabaseModel ToRepositoryDatabase(in Repository repository)
            {
                return new RepositoryDatabaseModel
                {
                    DataDownloadedAt = repository.DataDownloadedAt,
                    Description = repository.Description,
                    StarCount = repository.StarCount,
                    Url = repository.Url,
                    IssuesCount = repository.IssuesCount,
                    ForkCount = repository.ForkCount,
                    Name = repository.Name,
                    OwnerAvatarUrl = repository.OwnerAvatarUrl,
                    OwnerLogin = repository.OwnerLogin,
                    IsFork = repository.IsFork,
                    TotalClones = repository.TotalClones,
                    TotalUniqueClones = repository.TotalUniqueClones,
                    TotalViews = repository.TotalViews,
                    TotalUniqueViews = repository.TotalUniqueViews,
                };
            }
        }
    }
}
