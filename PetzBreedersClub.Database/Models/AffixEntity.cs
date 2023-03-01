﻿#nullable disable
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using PetzBreedersClub.Database.Models.Base;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PetzBreedersClub.Database.Models.Enums;

namespace PetzBreedersClub.Database.Models;

[Table("Affixes")]
public class AffixEntity : Entity
{
	[Required]
	public string Name { get; set; }

	[Required]
	public AffixSyntax AffixSyntax { get; set; }

	[Required]
	public int OwnerId { get; set; }
	public virtual MemberEntity Owner { get; set; }
}

public class AffixEntityConfiguration : IEntityTypeConfiguration<AffixEntity>
{
	public void Configure(EntityTypeBuilder<AffixEntity> builder)
	{
		builder
			.Property(a => a.AffixSyntax);

		builder
			.HasIndex(a => a.Name).IsUnique();

		builder
			.Property(a => a.AffixSyntax)
			.HasConversion(new EnumToStringConverter<AffixSyntax>());

		builder
			.HasOne(a => a.Owner)
			.WithMany(o => o.Affixes)
			.HasForeignKey(a => a.OwnerId);
	}
}
#nullable enable