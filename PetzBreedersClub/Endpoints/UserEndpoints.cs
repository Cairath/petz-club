﻿using Microsoft.AspNetCore.Mvc;
using PetzBreedersClub.DTOs.User;
using PetzBreedersClub.Services;
using PetzBreedersClub.Services.Auth;

namespace PetzBreedersClub.Endpoints;

public static class UserEndpoints
{
	public static void MapUserEndpoints(this IEndpointRouteBuilder routes)
	{
		var group = routes.MapGroup("/api/users").WithTags("Users");

		group.MapPost("/register", async (RegistrationForm registrationForm, IUserService userService) =>
			{
				return await userService.RegisterNewUser(registrationForm);
			})
			.WithName("RegisterUser")
			.WithOpenApi();

		group.MapPost("/sign-in", async (UserSignIn user, IUserService userService) =>
			{
				return await userService.SignIn(user);
			})
			.WithName("SignIn")
			.Produces<SignedInUserInfo>()
			.WithOpenApi();

		group.MapPost("/sign-out", async (IUserService userService) =>
			{
				 return await userService.SignOut();
			})
			.WithName("SignOut")
			.WithOpenApi();

		group.MapPost("/authenticate", ([FromBody]int userId, HttpContext httpContext, IUserService userService) =>
			{
				return httpContext.User.Identity?.IsAuthenticated ?? userService.GetUserId() == userId.ToString();
			})
			.WithName("Authenticate")
			.Produces<bool>()
			.WithOpenApi();

		group.MapGet("/notifications", async (INotificationService notificationService) =>
			{
				return await notificationService.GetUserNotifications();
			})
			.WithName("GetUserNotifications")
			.Produces<List<Notification>>()
			.WithOpenApi();

#if DEBUG
		group.MapPost("/notifications/add", async (AddNotification form, INotificationService notificationService) =>
			{
				await notificationService.AddNotification(new List<int>(){ form.UserId}, text: form.Text, form.Type);
			})
			.WithName("DEV Add Notification")
			.WithOpenApi();
#endif
	}
}