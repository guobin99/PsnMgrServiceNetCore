using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PsnMgrService.Models;

namespace PsnMgrService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ST_DepartmentController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ST_DepartmentController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/ST_Department
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ST_Department>>> GetST_Departments()
        {
            return await _context.ST_Departments.ToListAsync();
        }

        // GET: api/ST_Department/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ST_Department>> GetST_Department(int id)
        {
            var sT_Department = await _context.ST_Departments.FindAsync(id);

            if (sT_Department == null)
            {
                return NotFound();
            }

            return sT_Department;
        }

        // PUT: api/ST_Department/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutST_Department(int id, ST_Department sT_Department)
        {
            if (id != sT_Department.Dept_ID)
            {
                return BadRequest();
            }

            _context.Entry(sT_Department).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ST_DepartmentExists(id))
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

        // POST: api/ST_Department
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ST_Department>> PostST_Department(ST_Department sT_Department)
        {
            _context.ST_Departments.Add(sT_Department);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (ST_DepartmentExists(sT_Department.Dept_ID))
                {
                    return Conflict();
                }
                else
                {
                    throw;
                }
            }

            return CreatedAtAction("GetST_Department", new { id = sT_Department.Dept_ID }, sT_Department);
        }

        // DELETE: api/ST_Department/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteST_Department(int id)
        {
            var sT_Department = await _context.ST_Departments.FindAsync(id);
            if (sT_Department == null)
            {
                return NotFound();
            }

            _context.ST_Departments.Remove(sT_Department);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ST_DepartmentExists(int id)
        {
            return _context.ST_Departments.Any(e => e.Dept_ID == id);
        }
    }
}
