using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Claims_Manager.Models;

namespace Claims_Manager.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class JobsController : ControllerBase
    {
        private readonly JobContext _context;

        public JobsController(JobContext context)
        {
            _context = context;
        }

        // GET: api/Jobs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Job>>> GetJobItems()
        {
          if (_context.TodoItems == null)
          {
              return NotFound();
          }

            foreach (var entity in _context.ChangeTracker.Entries().ToList())
            {
                _context.Entry(entity.Entity).State = EntityState.Detached;
            }


            return await _context.TodoItems.ToListAsync();
        }

        // GET: api/Jobs/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Job>> GetJob(long id)
        {
          if (_context.TodoItems == null)
          {
              return NotFound();
          }
            var job = await _context.TodoItems.FindAsync(id);

            if (job == null)
            {
                return NotFound();
            }

            
            return job;
        }

        // PUT: api/Jobs/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutJob(long id, Job job)
        {
            if (id != job.Id)
            {
                return BadRequest();
            }

            _context.Entry(job).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!JobExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Jobs
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Job>> PostJob(Job job)
        {
          if (_context.TodoItems == null)
          {
              return Problem("Entity set 'JobContext.TodoItems'  is null.");
          }
            job = new Job();
            _context.TodoItems.Add(job);
            await _context.SaveChangesAsync();

            // return CreatedAtAction("GetJob", new { id = job.Id }, job);

            return CreatedAtAction(nameof(PostJob), new { id = job.Id }, job);
        }

        // POST: api/Jobs/1
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Job>> UpdateClaimsProcessed(int id)
        {
            var job = await _context.TodoItems.FirstOrDefaultAsync(j => j.Id == id);
            if (job == null)
            {
                return Problem("Entity set 'JobContext.TodoItems'  is null.");
            }
            job.claimsProcessed++; // Increment claimsProcessed

            await _context.SaveChangesAsync(); // Save changes to database

            return CreatedAtAction(nameof(UpdateClaimsProcessed), new { id }, job);
        }

        // POST: api/Jobs
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Job>> UpdateCpuUsage(int id, int NewUsage)
        {
            var job = await _context.TodoItems.FirstOrDefaultAsync(j => j.Id == id);
            if (job == null)
            {
                return Problem("Entity set 'JobContext.TodoItems'  is null.");
            }
            job.cpuUsage += NewUsage;
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(PostJob), id, job);
            
        }

        // POST: api/Jobs
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Job>> UpdateMemoryUsage(int id, int NewUsage)
        {
            var job = await _context.TodoItems.FirstOrDefaultAsync(j => j.Id == id);
            if (job == null)
            {
                return Problem("Entity set 'JobContext.TodoItems'  is null.");
            }
            job.memoryUsage += NewUsage;
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(PostJob), id, job);
        }

        // POST: api/Jobs
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Job>> EndJob(int id)
        {
            var job = await _context.TodoItems.FirstOrDefaultAsync(j => j.Id == id);
            if (job == null)
            {
                return Problem("Entity set 'JobContext.TodoItems'  is null.");
            }
            job.endJob();
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(PostJob), id, job);
        }

        // DELETE: api/Jobs/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteJob(long id)
        {
            if (_context.TodoItems == null)
            {
                return NotFound();
            }
            var job = await _context.TodoItems.FindAsync(id);
            if (job == null)
            {
                return NotFound();
            }

            _context.TodoItems.Remove(job);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool JobExists(long id)
        {
            return (_context.TodoItems?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
