using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : Controller
{
    private readonly IUserRepository userRepository;
    private readonly IMapper mapper;

    // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
    public UsersController(IUserRepository userRepository, IMapper mapper)
    {
        this.userRepository = userRepository;
        this.mapper = mapper;
    }

    [Produces("application/json", "application/xml")]
    [HttpGet("{userId}", Name = nameof(GetUserById))]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {
        var userEntity = userRepository.FindById(userId);
        if (userEntity is null)
        {
            return NotFound();
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
        if (user.Login?.All(char.IsLetterOrDigit) != true)
        {
            ModelState.AddModelError(nameof(user.Login), "Login must contain letters or digits");
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
}