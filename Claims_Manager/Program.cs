using Microsoft.EntityFrameworkCore;
using Claims_Manager.Models;
using Claims_Manager.Services;
using MongoDB.Driver;
using MongoDB.Bson;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.Configure<JobTrackerDatabaseSettings>(builder.Configuration.GetSection("JobTrackerDatabase"));

builder.Services.AddSingleton<JobsService>();

builder.Services.AddControllers();
// builder.Services.AddDbContext<JobContext>(opt =>
// opt.UseInMemoryDatabase("TodoList"));


builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "JobTrackerApi", Version = "v1" });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (builder.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger(); // UseSwaggerUI Protected by if (env.IsDevelopment())
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "TodoApi v1"));
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();