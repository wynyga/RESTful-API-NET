using Business.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Models.DTOs;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Every signed-in user may read projects; only Admins may change them.
    public class ProjectsController : ControllerBase
    {
        private const string NotFoundMessage = "Project tidak ditemukan atau ID tidak valid.";

        private readonly IProjectService _projectService;

        public ProjectsController(IProjectService projectService)
        {
            _projectService = projectService;
        }

        /// <summary>
        /// A page of projects. Query: search, status (On Progress | Completed | Overdue),
        /// sortBy (name | status | startDate | endDate | progress), sortOrder (asc | desc), page, pageSize (max 100).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] ProjectQueryDTO query, CancellationToken ct)
        {
            return Ok(await _projectService.GetProjectsAsync(query, ct));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id, CancellationToken ct)
        {
            var project = await _projectService.GetProjectByIdAsync(id, ct);
            if (project == null)
            {
                return Problem(detail: NotFoundMessage, statusCode: StatusCodes.Status404NotFound);
            }

            return Ok(project);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] ProjectRequestDTO request, CancellationToken ct)
        {
            try
            {
                var createdProject = await _projectService.CreateProjectAsync(request, ct);
                return CreatedAtAction(nameof(GetById), new { id = createdProject?.Id }, createdProject);
            }
            catch (InvalidOperationException ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(string id, [FromBody] ProjectRequestDTO request, CancellationToken ct)
        {
            try
            {
                var updatedProject = await _projectService.UpdateProjectAsync(id, request, ct);
                if (updatedProject == null)
                {
                    return Problem(detail: NotFoundMessage, statusCode: StatusCodes.Status404NotFound);
                }

                return Ok(updatedProject);
            }
            catch (InvalidOperationException ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id, CancellationToken ct)
        {
            var success = await _projectService.DeleteProjectAsync(id, ct);
            if (!success)
            {
                return Problem(detail: NotFoundMessage, statusCode: StatusCodes.Status404NotFound);
            }

            return Ok(new { Message = "Project berhasil dihapus." });
        }
    }
}
