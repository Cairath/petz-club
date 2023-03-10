using PetzBreedersClub.Database;
using PetzBreedersClub.DTOs.Affixes;
using PetzBreedersClub.Services.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PetzBreedersClub.Database.Models;
using PetzBreedersClub.Database.Models.Enums;
using PetzBreedersClub.DTOs.Pets;

namespace PetzBreedersClub.Services;

public interface IPetService
{
	Task<IResult> GetPetProfile(int petId);
	Task<IResult> GetPedigree(int petId, int generations);
}

public class PetService : IPetService
{
	private readonly Context _context;
	private readonly IUserService _userService;
	private readonly IMemoryCache _cache;
	private readonly AffixRegistrationFormValidator _affixRegistrationFormValidator;

	public PetService(Context context, IUserService userService, IMemoryCache cache, AffixRegistrationFormValidator affixRegistrationFormValidator)
	{
		_context = context;
		_userService = userService;
		_cache = cache;
		_affixRegistrationFormValidator = affixRegistrationFormValidator;
	}

	public async Task<IResult> GetPetProfile(int petId)
	{
		var petData =
			await _context.Pets
				.Include(p => p.Offspring)
				.Include(p => p.Affix)
				.Include(p => p.Breed)
				.Include(p => p.Owner)
				.Include(p => p.Breeder)
				.Where(p => p.Id == petId && p.Status != PetStatus.PendingRegistration)
				.Select(p => new
				{
					Id = p.Id,
					ShowName = p.ShowName,
					AffixId = p.AffixId,
					AffixName = p.Affix.Name,
					PedigreeNumber = p.PedigreeNumber,
					RegistrationDate = p.RegistrationDate,
					Age = p.Age,
					Sex = p.Sex,
					GameVersion = p.GameVersion,
					BreedId = p.BreedId,
					BreedName = p.Breed.Name,
					OwnerId = p.OwnerId,
					OwnerName = p.Owner.Name,
					BreederId = p.BreederId,
					BreederName = p.Breeder.Name,
					Offspring = p.Offspring.Select(o => new PetLink { Id = o.Id, ShowName = o.ShowName, Sex = o.Sex }).ToList(),
					SiblingsDam = p.Dam.Offspring.Where(o => o.Id != petId).Select(o => new SiblingLink { Id = o.Id, ShowName = o.ShowName, Sex = o.Sex }).ToList(),
					SiblingsSire = p.Sire.Offspring.Where(o => o.Id != petId).Select(o => new SiblingLink { Id = o.Id, ShowName = o.ShowName, Sex = o.Sex }).ToList()
				}).FirstOrDefaultAsync();

		if (petData == null)
		{
			return Results.NotFound();
		}

		var siblings = petData.SiblingsDam.UnionBy(petData.SiblingsSire, s => s.Id).ToList();

		foreach (var sibling in siblings)
		{
			if (petData.SiblingsDam.Any(sd => sd.Id == sibling.Id) && petData.SiblingsSire.Any(sd => sd.Id == sibling.Id))
			{
				sibling.Full = true;
			}
		}

		var petProfile = new PetProfileData
		{
			Id = petData.Id,
			ShowName = petData.ShowName,
			AffixId = petData.AffixId,
			AffixName = petData.AffixName,
			PedigreeNumber = petData.PedigreeNumber,
			RegistrationDate = petData.RegistrationDate!.Value,
			Age = petData.Age,
			Sex = petData.Sex,
			GameVersion = petData.GameVersion,
			BreedId = petData.BreedId,
			BreedName = petData.BreedName,
			OwnerId = petData.OwnerId,
			OwnerName = petData.OwnerName,
			BreederId = petData.BreederId,
			BreederName = petData.BreederName,
			Offspring = petData.Offspring,
			Siblings = siblings,
			Pedigree = await GetPedigreeData(petId, 3)
		};

		return Results.Ok(petProfile);
	}

	public async Task<IResult> GetPedigree(int petId, int generations)
	{
		var pedigreeData = await GetPedigreeData(petId, generations);

		return pedigreeData == null ? Results.NotFound() : Results.Ok(pedigreeData);
	}

	private async Task<Pedigree?> GetPedigreeData(int petId, int generations)
	{
		var pedigreeEntries = new List<List<PedigreeEntry?>>();
		var familyTree = new List<List<PetEntity?>>();

		var ancestors = await GetAncestors(petId, generations);

		if (ancestors.Count == 0)
		{
			return null;
		}

		for (var i = 0; i < generations; i++)
		{
			familyTree.Add(new List<PetEntity?>());
		}

		var pet = ancestors.First(p => p.Id == petId);

		for (var i = 0; i < generations; i++)
		{
			if (i == 0)
			{
				var sire = pet.Sire;
				var dam = pet.Dam;

				familyTree[0].Add(sire);
				familyTree[0].Add(dam);

				continue;
			}

			foreach (var p in familyTree[i - 1])
			{
				var sire = p?.Sire;
				var dam = p?.Dam;

				familyTree[i].Add(sire);
				familyTree[i].Add(dam);

			}
		}

		foreach (var generation in familyTree)
		{
			var pedigreeGen = new List<PedigreeEntry?>();

			foreach (var entry in generation)
			{
				var pedigreeEntry = entry != null
					? new PedigreeEntry
					{ Id = entry.Id, PedigreeNumber = entry.PedigreeNumber, ShowName = entry.ShowName }
					: null;

				pedigreeGen.Add(pedigreeEntry);
			}

			pedigreeEntries.Add(pedigreeGen);
		}

		return new Pedigree { Entries = pedigreeEntries };
	}

	private Task<List<PetEntity>> GetAncestors(int petId, int generations)
	{
		return _context.Pets.FromSqlRaw($@"
					WITH Ancestors (
					    {nameof(PetEntity.Id)}, 
					    {nameof(PetEntity.ShowName)}, 
					    {nameof(PetEntity.PartialShowName)}, 
					    {nameof(PetEntity.CallName)}, 
					    {nameof(PetEntity.PedigreeNumber)}, 
					    {nameof(PetEntity.RegistrationDate)}, 
					    {nameof(PetEntity.RegistrarId)}, 
					    {nameof(PetEntity.Age)}, 
					    {nameof(PetEntity.Sex)}, 
					    {nameof(PetEntity.GameVersion)}, 
					    {nameof(PetEntity.Status)}, 
					    {nameof(PetEntity.SireId)}, 
					    {nameof(PetEntity.DamId)}, 
					    {nameof(PetEntity.AffixId)}, 
					    {nameof(PetEntity.BreedId)}, 
					    {nameof(PetEntity.OwnerId)}, 
					    {nameof(PetEntity.BreederId)}, 
					    {nameof(PetEntity.CreatedDate)}, 
					    {nameof(PetEntity.AddedBy)}, 
					    {nameof(PetEntity.LastModifiedDate)}, 
					    {nameof(PetEntity.ModifiedBy)}, 
					    Depth
					) AS (
					    SELECT 
						    {nameof(PetEntity.Id)}, 
						    {nameof(PetEntity.ShowName)}, 
						    {nameof(PetEntity.PartialShowName)}, 
						    {nameof(PetEntity.CallName)}, 
						    {nameof(PetEntity.PedigreeNumber)}, 
							{nameof(PetEntity.RegistrationDate)}, 
							{nameof(PetEntity.RegistrarId)}, 
						    {nameof(PetEntity.Age)}, 
							{nameof(PetEntity.Sex)}, 
							{nameof(PetEntity.GameVersion)}, 
							{nameof(PetEntity.Status)}, 
						    {nameof(PetEntity.SireId)}, 
						    {nameof(PetEntity.DamId)}, 
						    {nameof(PetEntity.AffixId)}, 
						    {nameof(PetEntity.BreedId)}, 
						    {nameof(PetEntity.OwnerId)}, 
						    {nameof(PetEntity.BreederId)}, 
						    {nameof(PetEntity.CreatedDate)}, 
						    {nameof(PetEntity.AddedBy)}, 
						    {nameof(PetEntity.LastModifiedDate)}, 
						    {nameof(PetEntity.ModifiedBy)}, 
					        0
					    FROM dbo.Pets
					    WHERE Id = {petId}

					    UNION ALL

					    SELECT 
					        p.{nameof(PetEntity.Id)}, 
					        p.{nameof(PetEntity.ShowName)}, 
					        p.{nameof(PetEntity.PartialShowName)}, 
					        p.{nameof(PetEntity.CallName)}, 
					        p.{nameof(PetEntity.PedigreeNumber)},
							p.{nameof(PetEntity.RegistrationDate)}, 
							p.{nameof(PetEntity.RegistrarId)}, 
					        p.{nameof(PetEntity.Age)},
							p.{nameof(PetEntity.Sex)}, 
							p.{nameof(PetEntity.GameVersion)}, 
					        p.{nameof(PetEntity.Status)}, 
					        p.{nameof(PetEntity.SireId)}, 
					        p.{nameof(PetEntity.DamId)}, 
					        p.{nameof(PetEntity.AffixId)}, 
					        p.{nameof(PetEntity.BreedId)}, 
					        p.{nameof(PetEntity.OwnerId)}, 
					        p.{nameof(PetEntity.BreederId)}, 
					        p.{nameof(PetEntity.CreatedDate)}, 
					        p.{nameof(PetEntity.AddedBy)}, 
					        p.{nameof(PetEntity.LastModifiedDate)},  
					        p.{nameof(PetEntity.ModifiedBy)}, 
					        pa.Depth + 1
					    FROM 
					        dbo.Pets AS p
					        INNER JOIN Ancestors AS pa ON p.{nameof(PetEntity.Id)} = pa.{nameof(PetEntity.SireId)}

					    UNION ALL

					    SELECT 
					        p.{nameof(PetEntity.Id)}, 
					        p.{nameof(PetEntity.ShowName)}, 
					        p.{nameof(PetEntity.PartialShowName)}, 
					        p.{nameof(PetEntity.CallName)}, 
					        p.{nameof(PetEntity.PedigreeNumber)}, 
							p.{nameof(PetEntity.RegistrationDate)}, 
							p.{nameof(PetEntity.RegistrarId)}, 
					        p.{nameof(PetEntity.Age)},
							p.{nameof(PetEntity.Sex)}, 
							p.{nameof(PetEntity.GameVersion)}, 
					        p.{nameof(PetEntity.Status)}, 
					        p.{nameof(PetEntity.SireId)}, 
					        p.{nameof(PetEntity.DamId)}, 
					        p.{nameof(PetEntity.AffixId)}, 
					        p.{nameof(PetEntity.BreedId)}, 
					        p.{nameof(PetEntity.OwnerId)}, 
					        p.{nameof(PetEntity.BreederId)}, 
					        p.{nameof(PetEntity.CreatedDate)}, 
					        p.{nameof(PetEntity.AddedBy)}, 
					        p.{nameof(PetEntity.LastModifiedDate)}, 
					        p.{nameof(PetEntity.ModifiedBy)}, 
					        pa.Depth + 1
					    FROM 
					        dbo.Pets AS p
					        INNER JOIN Ancestors AS pa ON p.{nameof(PetEntity.Id)} = pa.{nameof(PetEntity.DamId)}
					)
					SELECT * FROM Ancestors WHERE Depth <= {generations}")
			.AsNoTrackingWithIdentityResolution()
			.ToListAsync();
	}
}