﻿#nullable disable
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using PetzBreedersClub.Database.Models.Base;

namespace PetzBreedersClub.Database.Models;

[Table("Users")]
public class UserEntity : Entity
{
	[Required]
	public string Email { get; set; }
	[Required]
	public string PasswordHash { get; set; }
	
	public virtual MemberEntity Member { get; set; }
	public virtual ICollection<SystemNotificationEntity> SystemNotifications { get; set; } = new List<SystemNotificationEntity>();

}

public class UserEntityConfiguration : IEntityTypeConfiguration<UserEntity>
{
	public void Configure(EntityTypeBuilder<UserEntity> builder)
	{
		builder
			.HasIndex(u => u.Email).IsUnique();
	}
}
#nullable enable