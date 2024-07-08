using Microsoft.AspNetCore.Authorization.Infrastructure;
using System.Collections;
using Cassandra.Mapping.Attributes;
using MongoDB.Bson.Serialization.Attributes;


namespace Claims_Manager.Models
{
    /*
     * Each service should implememnt this job class?
     * If not implement, Job class should have a new field with type service which
     * holds information about which service the job is from.
     * Does each service report to Program.cs?
     * If so, Program.cs should contain a List<Job> field where each Job is pushed by the 
     * service by Api call, and program should flush these details to db periodically
     * Other stats could be most frequent services, services using most cpu/mem
     * and average cpu/mem per service instead of job
     */
    [BsonIgnoreExtraElements]
    public class Job
    {
        [BsonId]
        [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
        public String Id { get; set; }
        // Is the service currently running
        [BsonElement("runningStatus")]
        public bool runningStatus { get; set; }

        // Timestamp for when the service starts
        [BsonElement("startTime")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime startTime { get; set; }

        // Timestamp for when the service ends
        [BsonElement("endTime")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime endTime { get; set; }

        // Placeholder type, should capture cpu used to process claims
        [BsonElement("cpuUsage")]
        public double? cpuUsage { get; set; }

        // Placeholder type, should capture memory used to process claims
        [BsonElement("memoryUsage")]
        public double? memoryUsage { get; set; }

        // Claims processed
        [BsonElement("claimsProcessed")]
        public int? claimsProcessed { get; set; }

        public Job()
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
            runningStatus = true;
            startTime = DateTime.Now; // datetime object in current local time
            cpuUsage = 0;
            memoryUsage = 0;
            claimsProcessed = 0;
        }

        public void endJob()
        {
            runningStatus = false;
            endTime = DateTime.Now;
        }

        public double? GetAverageCpuUsage()
        {
            return claimsProcessed / cpuUsage;
        }
        public double? GetAverageMemoryUsage()
        {
            return claimsProcessed / memoryUsage;
        }
    }
}
