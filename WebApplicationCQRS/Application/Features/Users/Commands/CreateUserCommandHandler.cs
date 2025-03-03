using System.Net;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WebApplicationCQRS.Domain;
using WebApplicationCQRS.Domain.Entities;
using WebApplicationCQRS.Domain.Interfaces;

// == service
namespace WebApplicationCQRS.Application.Features.Users.Commands;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<int>>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(IUserRepository userRepository, ILogger<CreateUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }


    public async Task<Result<int>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetUserByUsername(request.Name);
            if (user != null)
            {
                return Result<int>.Failure(ResponseCode.Conflict, "User already exists", HttpStatusCode.Conflict);
            }

            var id = Random.Shared.Next(1, 100);
            var userModel = new User(id, request.Name, request.Email,
                BCrypt.Net.BCrypt.HashPassword(request.Password),
                DateOnly.FromDateTime(DateTime.Now),
                DateTime.Now, DateTime.Now);

            await _userRepository.CreateUser(userModel);
            return Result<int>.Success(userModel.Id, "User Created Successfully");
        }
        catch (DbUpdateException dbEx) when (dbEx.InnerException?.Message.Contains("UQ_UserName") == true)
        {
            _logger.LogWarning("Duplicate username detected: {Name}", request.Name);
            return Result<int>.Failure(ResponseCode.Conflict, "User already exists", HttpStatusCode.Conflict);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unexpected error while creating user.");
            return Result<int>.Failure(ResponseCode.InternalError, "Internal Server Error", HttpStatusCode.InternalServerError);
        }

    }

}