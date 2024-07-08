using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Claims_Manager.Models;
using Claims_Manager.Services;

namespace Claims_Manager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobsController : ControllerBase
    {
        private readonly JobsService _context;

        public JobsController(JobsService context) => _context = context;

        [HttpGet]
        public async Task<List<Job>> Get() =>
        await _context.GetAsync();

        [HttpGet("{id:length(24)}")]
        public async Task<ActionResult<Job>> Get(string id)
        {
            var Job = await _context.GetAsync(id);

            if (Job is null)
            {
                return NotFound();
            }

            return Job;
        }

        [HttpPost]
        public async Task<IActionResult> Post(Job newJob)
        {
            await _context.CreateAsync(newJob);

            return CreatedAtAction(nameof(Get), new { id = newJob.Id }, newJob);
        }

        [HttpPut("{id:length(24)}")]
        public async Task<IActionResult> Update(string id, Job updatedJob)
        {
            var Job = await _context.GetAsync(id);

            if (Job is null)
            {
                return NotFound();
            }

            updatedJob.Id = Job.Id;

            await _context.UpdateAsync(id, updatedJob);

            return NoContent();
        }

        [HttpDelete("{id:length(24)}")]
        public async Task<IActionResult> Delete(string id)
        {
            var Job = await _context.GetAsync(id);

            if (Job is null)
            {
                return NotFound();
            }

            await _context.RemoveAsync(id);

            return NoContent();
        }
    }
}
