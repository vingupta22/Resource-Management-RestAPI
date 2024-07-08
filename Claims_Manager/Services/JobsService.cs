using Claims_Manager.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System.Configuration;

namespace Claims_Manager.Services
{
    public class JobsService
    {
        private readonly IMongoCollection<Job> _jobsCollection;

        public JobsService(
            IOptions<JobTrackerDatabaseSettings> jobTrackerDatabaseSettings)
        {
            var mongoClient = new MongoClient(
                jobTrackerDatabaseSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                jobTrackerDatabaseSettings.Value.DatabaseName);

            _jobsCollection = mongoDatabase.GetCollection<Job>(
                jobTrackerDatabaseSettings.Value.JobsCollectionName);
        }

        public async Task<List<Job>> GetAsync() =>
            await _jobsCollection.Find(_ => true).ToListAsync();

        public async Task<Job?> GetAsync(String id) =>
            await _jobsCollection.Find(x => x.Id.ToString() ==  (id)).FirstOrDefaultAsync();

        public async Task CreateAsync(Job newJob) =>
            await _jobsCollection.InsertOneAsync(newJob);

        public async Task UpdateAsync(String id, Job updatedJob) =>
            await _jobsCollection.ReplaceOneAsync(x => x.Id.ToString() == (id), updatedJob);

        public async Task RemoveAsync(String id) =>
            await _jobsCollection.DeleteOneAsync(x => x.Id.ToString() == (id));

    }
}
