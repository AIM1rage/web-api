using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;
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

    /// <summary>
    /// Получить пользователя
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <returns></returns>
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

    /// <summary>
    /// Получить список пользователей с пагинацией
    /// </summary>
    /// <param name="pageNumber">Номер страницы</param>
    /// <param name="pageSize">Размер страницы</param>
    /// <returns>Список пользователей с информацией о пагинации в заголовках</returns>
    [SwaggerResponse(200, "Возвращает список пользователей", typeof(IEnumerable<UserDto>))]
    [HttpGet]
    [Produces("application/json", "application/xml")]
    public ActionResult<IEnumerable<UserDto>> GetUsers(
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

    /// <summary>
    /// Получить поддерживаемые методы HTTP для данного endpoint
    /// </summary>
    /// <returns>Информация о доступных HTTP методах</returns>
    [SwaggerResponse(200, "Возвращает список доступных методов в заголовке Allow")]
    [HttpOptions]
    public IActionResult Options()
    {
        var allowedMethods = new[] { HttpMethods.Get, HttpMethods.Post, HttpMethods.Options };
        Response.Headers.Allow = string.Join(", ", allowedMethods);
        return Ok();
    }

    /// <summary>
    /// Обновить или создать пользователя
    /// </summary>
    /// <param name="updateUserDto">Данные для обновления пользователя</param>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <returns>Результат операции обновления</returns>
    [SwaggerResponse(204, "Пользователь успешно обновлен")]
    [SwaggerResponse(201, "Пользователь создан")]
    [SwaggerResponse(400, "Неверные входные данные")]
    [SwaggerResponse(422, "Ошибка валидации данных")]
    [Produces("application/json", "application/xml")]
    [HttpPut("{userId}")]
    public IActionResult UpdateUser([FromBody] UpdateUserDto updateUserDto, [FromRoute] Guid userId)
    {
        if (updateUserDto is null || userId == Guid.Empty)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        var createdUserEntity = mapper.Map(new UserEntity(userId), mapper.Map<UserEntity>(updateUserDto));

        userRepository.UpdateOrInsert(createdUserEntity, out var isInsert);

        if (!isInsert)
        {
            return NoContent();
        }

        return CreatedAtRoute(nameof(GetUserById), new { userId = createdUserEntity.Id }, createdUserEntity.Id);
    }

    [Produces("application/json", "application/xml")]
    [HttpPatch("{userId:guid}")]
    public IActionResult PartiallyUpdateUser([FromRoute] Guid userId, [FromBody] JsonPatchDocument<UpdateUserDto> patchDoc)
    {
        if (patchDoc is null)
        {
            return BadRequest();
        }
        var userEntity = userRepository.FindById(userId);
        if (userEntity is null)
        {
            return NotFound();
        }
        var updateUserDto = mapper.Map<UpdateUserDto>(userEntity);
        patchDoc.ApplyTo(updateUserDto, ModelState);
        if (!TryValidateModel(updateUserDto))
        {
            return UnprocessableEntity(ModelState);
        }
        userRepository.Update(mapper.Map<UserEntity>(updateUserDto));
        return NoContent();
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