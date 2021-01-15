﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Logging;
using sfa.Tl.Marketing.Communication.Application.Interfaces;
using sfa.Tl.Marketing.Communication.Models.Entities;

namespace sfa.Tl.Marketing.Communication.Application.Repositories
{
    public class GenericCloudTableRepository<T, TKey> : ICloudTableRepository<T>
        where T : Entity<TKey>, ITableEntity, new()
    {
        private readonly CloudTableClient _cloudTableClient;
        private readonly ILogger<GenericCloudTableRepository<T, TKey>> _logger;

        private readonly string _tableName;

        public GenericCloudTableRepository(
            CloudTableClient cloudTableClient,
            ILogger<GenericCloudTableRepository<T, TKey>> logger)
        {
            _cloudTableClient = cloudTableClient ?? throw new ArgumentNullException(nameof(cloudTableClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _tableName = typeof(T).Name;
            const string suffix = "Entity";
            if (_tableName.EndsWith(suffix))
            {
                _tableName = _tableName.Remove(_tableName.Length - suffix.Length);
            }
        }

        public async Task<IList<T>> GetAll()
        {
            var results = new List<T>();

            var cloudTable = _cloudTableClient.GetTableReference(_tableName);
            if (!cloudTable.Exists())
            {
                _logger.LogWarning($"GetAll from table - {_tableName} not found. Returning 0 results.");
                return results;
            }

            var tableQuery = new TableQuery<T>();
            var continuationToken = default(TableContinuationToken);

            var loopCount = 0;
            var stopwatch = Stopwatch.StartNew();

            do
            {
                var queryResults = await cloudTable
                    .ExecuteQuerySegmentedAsync(
                        tableQuery,
                        continuationToken);

                continuationToken = queryResults.ContinuationToken;

                results.AddRange(queryResults.Results);
                loopCount++;
            } while (continuationToken != null);

            stopwatch.Stop();
            _logger.LogInformation($"GetAll from {_tableName} returning {results.Count} results from {loopCount} loops in {stopwatch.ElapsedMilliseconds:#,###}ms.");  

            return results;
        }

        public async Task<int> Save(IList<T> entities)
        {
            if (entities == null || !entities.Any())
            {
                _logger.LogInformation($"Save to {_tableName} was given no entities to save.");
                return 0;
            }

            var cloudTable = _cloudTableClient.GetTableReference(_tableName);

            await cloudTable.CreateIfNotExistsAsync();

            var inserted = 0;
            var batchCount = 0;
            const int batchSize = 100;
            var stopwatch = Stopwatch.StartNew();

            var batchPartitionKey = entities.First().Id.ToString();

            var rowOffset = 0;

            while (rowOffset < entities.Count)
            {
                var batchOperation = new TableBatchOperation();

                // next batch
                var batchEntities = entities.Skip(rowOffset).Take(batchSize).ToList();

                foreach (var entity in batchEntities)
                {
                    //TODO: Check if exists, then update if it has changed
                    //TODO: Add a ctor with row key? or always do this in the entity?
                    entity.RowKey = entity.Id.ToString();
                    //TODO: Do we need a partition?
                    entity.PartitionKey = batchPartitionKey;

                    //TODO: Sort out object collections
                    // https://damieng.com/blog/2015/06/27/table-per-hierarchy-in-azure-table-storage
                    // https://stackoverflow.com/questions/19885219/insert-complex-objects-to-azure-table-with-tableserviceentity
                    // https://www.devprotocol.com/azure-table-storage-and-complex-types-stored-in-json/

                    batchOperation.InsertOrReplace(entity);
                }

                var batchResult = await cloudTable.ExecuteBatchAsync(batchOperation);
                inserted += batchResult.Count;
                batchCount++;

                rowOffset += batchEntities.Count;

                _logger.LogInformation($"Save to {_tableName} batch result {batchResult.Count} entities in rowOffset is now {rowOffset} batches in {stopwatch.ElapsedMilliseconds:#,###}ms.");

            }

            //TODO: Can do a batch insert
            //https://docs.microsoft.com/en-us/azure/cosmos-db/table-storage-how-to-use-java
            //Performance
            //https://stackoverflow.com/questions/17955557/painfully-slow-azure-table-insert-and-delete-batch-operations

            stopwatch.Stop();
            _logger.LogInformation($"Save to {_tableName} saved {inserted} entities in {batchCount} batches in {stopwatch.ElapsedMilliseconds:#,###}ms.");

            //return inserted;
            return inserted;
        }
    }
}