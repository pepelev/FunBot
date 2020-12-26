﻿using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;
using FunBot.Collections;
using FunBot.Jobs;
using FunBot.Storage;
using Serilog;

namespace FunBot.Updates
{
    public sealed class MoviesUpdate : Job<IReadOnlyList<Movie>>
    {
        private readonly ILogger log;
        private readonly SQLiteConnection connection;
        private readonly CancellationToken token;

        public MoviesUpdate(ILogger log, SQLiteConnection connection, CancellationToken token)
        {
            this.log = log;
            this.connection = connection;
            this.token = token;
        }

        public override Task RunAsync(IReadOnlyList<Movie> downloaded)
        {
            var stored = Stored();
            var comparison = Full.Join(stored, downloaded, movie => movie.Id);
            using var transaction = connection.BeginTransaction();
            try
            {
                if (Update(comparison, transaction))
                {
                    transaction.Commit();
                    return Task.CompletedTask;
                }
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            transaction.Rollback();
            return Task.CompletedTask;
        }

        private bool Update(
            IEnumerable<(string Key, Movie Old, Movie New)> comparison,
            SQLiteTransaction transaction)
        {
            foreach (var (key, old, @new) in comparison)
            {
                if (token.IsCancellationRequested)
                {
                    log.Information("Cancellation requested");
                    return false;
                }

                if (@new == null)
                {
                    transaction.Execute(
                        @"DELETE FROM TABLE movies WHERE id = :id",
                        ("id", key)
                    );
                    log.Information("Remove {Movie}", old);
                    continue;
                }

                if (old != @new)
                {
                    transaction.Execute(@"
                    REPLACE INTO movies (id, name, original_name, year)
                    VALUES (:id, :name, :original_name, :year)",
                        ("id", @new.Id),
                        ("name", @new.Name),
                        ("original_name", @new.OriginalName),
                        ("year", @new.Year)
                    );

                    log.Information("Replace {OldMovie} with {NewMovie}", old, @new);
                }
            }

            return true;
        }

        private IReadOnlyList<Movie> Stored() => connection.Read(@"
            SELECT id, name, original_name, year
            FROM `movies`
            ORDER BY id",
            row => new Movie(
                row.String("id"),
                row.String("name"),
                row.MaybeString("original_name"),
                row.MaybeInt("year")
            )
        );
    }
}