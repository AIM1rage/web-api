using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : Controller
{
    private readonly IUserRepository userRepository;
    private readonly IMapper mapper;
    private readonly LinkGenerator linkGenerator;

    // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
    public UsersController(IUserRepository userRepository, IMapper mapper, LinkGenerator linkGenerator)
    {
        this.userRepository = userRepository;
        this.mapper = mapper;
        this.linkGenerator = linkGenerator;
    }

    [Produces("application/json", "application/xml")]
    [HttpGet("{userId}", Name = nameof(GetUserById))]
    [HttpHead("{userId}")]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {
        var userEntity = userRepository.FindById(userId);
        if (userEntity is null)
        {
            return NotFound();
        }
        
        if (HttpMethods.IsHead(Request.Method))
        {
            var acceptHeader = Request.Headers.Accept.ToString();
            if (acceptHeader.Contains("application/xml"))
            {
                Response.Headers["Content-Type"] = "application/xml; charset=utf-8";
            }
            else
            {
                Response.Headers["Content-Type"] = "application/json; charset=utf-8";
            }
            return Ok();
        }
        return Ok(mapper.Map<UserDto>(userEntity));
    }

    [Produces("application/json", "application/xml")]
    [HttpPost]
    public IActionResult CreateUser([FromBody] CreateUserDto user)
    {
        if (user is null)
        {
            return BadRequest();
        }
        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }
        var createdUserEntity = userRepository.Insert(mapper.Map<UserEntity>(user));
        return CreatedAtRoute(
            nameof(GetUserById),
            new { userId = createdUserEntity.Id },
            createdUserEntity.Id);
    }
    
    [HttpGet]
    [Produces("application/json", "application/xml")]
    public ActionResult<IEnumerable<Models.UserDto>> GetUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        const int defaultPageNumber = 1;
        const int maxPageSize = 20;
        
        pageNumber = Math.Max(pageNumber, defaultPageNumber);
        pageSize = Math.Clamp(pageSize, 1, maxPageSize);
        
        var pageList = userRepository.GetPage(pageNumber, pageSize);
        var users = mapper.Map<IEnumerable<UserDto>>(pageList);
        
        var paginationHeader = new
        {
            previousPageLink = pageList.HasPrevious
                ? linkGenerator.GetUriByAction(HttpContext, nameof(GetUsers), values: new { pageNumber = pageNumber - 1, pageSize })
                : null,
            nextPageLink = pageList.HasNext
                ? linkGenerator.GetUriByAction(HttpContext, nameof(GetUsers), values: new { pageNumber = pageNumber + 1, pageSize })
                : null,
            totalCount = pageList.TotalCount,
            pageSize,
            currentPage = pageNumber,
            totalPages = (int)Math.Ceiling((double)pageList.TotalCount / pageSize)
        };

        Response.Headers["X-Pagination"] = JsonConvert.SerializeObject(paginationHeader);

        return Ok(users);
    }
    
    [HttpOptions]
    public IActionResult Options()
    {
        var allowedMethods = new[] { HttpMethods.Get, HttpMethods.Post, HttpMethods.Options };
        Response.Headers.Allow = string.Join(", ", allowedMethods);
        return Ok();
    }

    [HttpPut("{userId}")]
    public IActionResult UpsertUser([FromRoute] Guid userId, [FromBody] PutUserDto user)
    {
        if (user is null)
        {
            return BadRequest();
        }
        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }
        var userEntity = mapper.Map<UserEntity>(user);
        userEntity.Id = userId;
        userRepository.UpdateOrInsert(userEntity, out var isInserted);
        return isInserted ? CreatedAtRoute(nameof(GetUserById), new { userId = userId }, userId) : NoContent();
    }
    
    [Produces("application/json", "application/xml")]
    [HttpDelete("{userId}")]
    public IActionResult DeleteUser([FromRoute] Guid userId)
    {
        var userEntity = userRepository.FindById(userId);
        if (userEntity is null)
        {
            return NotFound();
        }
        
        userRepository.Delete(userId);
        return NoContent();
    }
}