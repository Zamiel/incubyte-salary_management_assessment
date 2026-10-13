using API.Data;
using API.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeesController(AppDbContext context) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<Employee>>> GetEmployees(
            [FromQuery] int pageNumber = 1, 
            [FromQuery] int pageSize = 20
        )
        {
            var employees = await context.Employees.ToListAsync();

            if (employees == null || employees.Count == 0)
            {
                return NotFound();
            }

            return Ok(employees);
        }
    }
}
