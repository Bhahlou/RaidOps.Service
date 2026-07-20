using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Calendar;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;

namespace RaidOps.Infrastructure.Persistence.Implementations;

/// <summary>
/// Entity Framework Core database context for RaidOps.
/// Exposes all domain sets and configures composite keys, unique indexes, and static seed data.
/// </summary>
public class RaidOpsDbContext(DbContextOptions<RaidOpsDbContext> options) : DbContext(options)
{
    // ── Discord / Guild ───────────────────────────────────────────────────

    /// <summary>Gets the <see cref="User"/> table.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Gets the <see cref="Guild"/> table.</summary>
    public DbSet<Guild> Guilds => Set<Guild>();

    /// <summary>Gets the <see cref="UserGuild"/> join table linking users to Discord guilds.</summary>
    public DbSet<UserGuild> UserGuilds => Set<UserGuild>();

    /// <summary>Gets the <see cref="GuildMembership"/> table linking characters to guild rosters.</summary>
    public DbSet<GuildMembership> GuildMemberships => Set<GuildMembership>();

    /// <summary>Gets the <see cref="GuildAuditLog"/> table recording guild action history.</summary>
    public DbSet<GuildAuditLog> GuildAuditLogs => Set<GuildAuditLog>();

    /// <summary>Gets the <see cref="NotificationDismissal"/> table tracking dismissed in-app notifications.</summary>
    public DbSet<NotificationDismissal> NotificationDismissals => Set<NotificationDismissal>();

    // ── Calendar ──────────────────────────────────────────────────────────

    /// <summary>Gets the <see cref="AvailabilityDeclaration"/> table (one-off availability declarations).</summary>
    public DbSet<AvailabilityDeclaration> AvailabilityExceptions => Set<AvailabilityDeclaration>();

    /// <summary>Gets the <see cref="RecurringAvailabilityPattern"/> table.</summary>
    public DbSet<RecurringAvailabilityPattern> RecurringAvailabilityPatterns => Set<RecurringAvailabilityPattern>();

    /// <summary>Gets the <see cref="RecurringAvailabilityPatternDay"/> table.</summary>
    public DbSet<RecurringAvailabilityPatternDay> RecurringAvailabilityPatternDays => Set<RecurringAvailabilityPatternDay>();

    // ── Static reference data ─────────────────────────────────────────────

    /// <summary>Gets the <see cref="Expansion"/> lookup table.</summary>
    public DbSet<Expansion> Expansions => Set<Expansion>();

    /// <summary>Gets the <see cref="Branch"/> lookup table (Retail, Classic Era, …).</summary>
    public DbSet<Branch> Branches => Set<Branch>();

    /// <summary>Gets the <see cref="Race"/> lookup table, keyed by Blizzard race ID.</summary>
    public DbSet<Race> Races => Set<Race>();

    /// <summary>Gets the <see cref="WowClass"/> lookup table, keyed by Blizzard class ID.</summary>
    public DbSet<WowClass> WowClasses => Set<WowClass>();

    /// <summary>Gets the <see cref="Spec"/> lookup table, keyed by Blizzard specialisation ID.</summary>
    public DbSet<Spec> Specs => Set<Spec>();

    // ── Runtime data ──────────────────────────────────────────────────────

    /// <summary>Gets the <see cref="Realm"/> table (on-demand BNet realm cache).</summary>
    public DbSet<Realm> Realms => Set<Realm>();

    /// <summary>Gets the <see cref="BattleNetAccount"/> table.</summary>
    public DbSet<BattleNetAccount> BattleNetAccounts => Set<BattleNetAccount>();

    /// <summary>Gets the <see cref="Character"/> table.</summary>
    public DbSet<Character> Characters => Set<Character>();

    /// <summary>Gets the <see cref="CharacterExpansionState"/> table.</summary>
    public DbSet<CharacterExpansionState> CharacterExpansionStates => Set<CharacterExpansionState>();

    /// <summary>Gets the <see cref="BnetCharacterSpec"/> join table.</summary>
    public DbSet<BnetCharacterSpec> BnetCharacterSpecs => Set<BnetCharacterSpec>();

    /// <summary>Gets the <see cref="CharacterRaidSpec"/> join table.</summary>
    public DbSet<CharacterRaidSpec> CharacterRaidSpecs => Set<CharacterRaidSpec>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureRelationships(modelBuilder);
        SeedStaticData(modelBuilder);
    }

    // ── Relationship configuration ────────────────────────────────────────

    private static void ConfigureRelationships(ModelBuilder modelBuilder)
    {
        // UserGuild — composite PK
        modelBuilder.Entity<UserGuild>()
            .HasKey(ug => new { ug.UserDiscordId, ug.GuildId });

        modelBuilder.Entity<UserGuild>()
            .HasOne(ug => ug.User)
            .WithMany(u => u.UserGuilds)
            .HasForeignKey(ug => ug.UserDiscordId);

        modelBuilder.Entity<UserGuild>()
            .HasOne(ug => ug.Guild)
            .WithMany(g => g.UserGuilds)
            .HasForeignKey(ug => ug.GuildId);

        // BattleNetAccount — composite PK (UserDiscordId, BnetId), many per User (no back-nav on User side)
        modelBuilder.Entity<BattleNetAccount>()
            .HasKey(b => new { b.UserDiscordId, b.BnetId });

        modelBuilder.Entity<BattleNetAccount>()
            .HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserDiscordId);

        // Character — FK to User, Realm, Race, WowClass
        modelBuilder.Entity<Character>()
            .HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserDiscordId);

        // Character.SourceBnetId — nullable composite FK to the BNet account it was synced from.
        // Cascade: unlinking a BNet account deletes every character sourced from it (and, via the
        // existing cascades below, their expansion states/specs/raid specs/guild memberships) —
        // a deliberate hard delete, not a deactivation, per GDPR "right to erasure" on unlink.
        modelBuilder.Entity<Character>()
            .HasOne<BattleNetAccount>()
            .WithMany()
            .HasForeignKey(c => new { c.UserDiscordId, c.SourceBnetId })
            .HasPrincipalKey(b => new { b.UserDiscordId, b.BnetId })
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Character>()
            .HasOne(c => c.Realm)
            .WithMany(r => r.Characters)
            .HasForeignKey(c => c.RealmId);

        modelBuilder.Entity<Character>()
            .HasOne(c => c.Race)
            .WithMany()
            .HasForeignKey(c => c.RaceId);

        modelBuilder.Entity<Character>()
            .HasOne(c => c.Class)
            .WithMany()
            .HasForeignKey(c => c.ClassId);

        // Unique: one character per (BnetCharacterId, RealmId)
        modelBuilder.Entity<Character>()
            .HasIndex(c => new { c.BnetCharacterId, c.RealmId })
            .IsUnique();

        // CharacterExpansionState — unique (CharacterId, ExpansionId)
        modelBuilder.Entity<CharacterExpansionState>()
            .HasOne(s => s.Character)
            .WithMany(c => c.ExpansionStates)
            .HasForeignKey(s => s.CharacterId);

        modelBuilder.Entity<CharacterExpansionState>()
            .HasOne(s => s.Expansion)
            .WithMany()
            .HasForeignKey(s => s.ExpansionId);

        modelBuilder.Entity<CharacterExpansionState>()
            .HasIndex(s => new { s.CharacterId, s.ExpansionId })
            .IsUnique();

        // BnetCharacterSpec — composite PK (CharacterExpansionStateId, SpecId, IsMain)
        // IsMain is included so Classic same-spec dual-spec (e.g. Ret/Ret) can be stored.
        modelBuilder.Entity<BnetCharacterSpec>()
            .HasKey(cs => new { cs.CharacterExpansionStateId, cs.SpecId, cs.IsMain });

        modelBuilder.Entity<BnetCharacterSpec>()
            .HasOne(cs => cs.CharacterExpansionState)
            .WithMany(s => s.Specs)
            .HasForeignKey(cs => cs.CharacterExpansionStateId);

        modelBuilder.Entity<BnetCharacterSpec>()
            .HasOne(cs => cs.Spec)
            .WithMany()
            .HasForeignKey(cs => cs.SpecId);

        // CharacterRaidSpec — composite PK (CharacterId, SpecId); user-curated, no IsMain-in-PK quirk needed
        modelBuilder.Entity<CharacterRaidSpec>()
            .HasKey(rs => new { rs.CharacterId, rs.SpecId });

        modelBuilder.Entity<CharacterRaidSpec>()
            .HasOne(rs => rs.Character)
            .WithMany(c => c.RaidSpecs)
            .HasForeignKey(rs => rs.CharacterId);

        modelBuilder.Entity<CharacterRaidSpec>()
            .HasOne(rs => rs.Spec)
            .WithMany()
            .HasForeignKey(rs => rs.SpecId);

        // GuildMembership — composite PK (CharacterId, GuildId)
        modelBuilder.Entity<GuildMembership>()
            .HasKey(m => new { m.CharacterId, m.GuildId });

        modelBuilder.Entity<GuildMembership>()
            .HasOne(m => m.Character)
            .WithMany(c => c.GuildMemberships)
            .HasForeignKey(m => m.CharacterId);

        modelBuilder.Entity<GuildMembership>()
            .HasOne(m => m.Guild)
            .WithMany(g => g.Memberships)
            .HasForeignKey(m => m.GuildId);

        // GuildAuditLog — FK to Guild; logs are immutable so no cascade delete
        modelBuilder.Entity<GuildAuditLog>()
            .HasOne(l => l.Guild)
            .WithMany()
            .HasForeignKey(l => l.GuildId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GuildAuditLog>()
            .HasIndex(l => new { l.GuildId, l.OccurredAt });

        // NotificationDismissal — composite PK (UserDiscordId, Type, GuildId)
        modelBuilder.Entity<NotificationDismissal>()
            .HasKey(nd => new { nd.UserDiscordId, nd.Type, nd.GuildId });

        modelBuilder.Entity<NotificationDismissal>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(nd => nd.UserDiscordId);

        // AvailabilityDeclaration — FK to User/Guild, no cascade (a departing member's history stays)
        modelBuilder.Entity<AvailabilityDeclaration>()
            .HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserDiscordId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AvailabilityDeclaration>()
            .HasOne(e => e.Guild)
            .WithMany()
            .HasForeignKey(e => e.GuildId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AvailabilityDeclaration>()
            .HasIndex(e => new { e.UserDiscordId, e.GuildId, e.StartDate, e.EndDate });

        // RecurringAvailabilityPattern — FK to User/Guild, no cascade; Days cascade with their pattern
        modelBuilder.Entity<RecurringAvailabilityPattern>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserDiscordId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RecurringAvailabilityPattern>()
            .HasOne(p => p.Guild)
            .WithMany()
            .HasForeignKey(p => p.GuildId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RecurringAvailabilityPattern>()
            .HasIndex(p => new { p.UserDiscordId, p.GuildId });

        modelBuilder.Entity<RecurringAvailabilityPatternDay>()
            .HasOne(d => d.Pattern)
            .WithMany(p => p.Days)
            .HasForeignKey(d => d.PatternId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RecurringAvailabilityPatternDay>()
            .HasIndex(d => new { d.PatternId, d.OffsetInCycle })
            .IsUnique();

        // Realm — unique (Slug, Region, BranchId), FK to Branch
        modelBuilder.Entity<Realm>()
            .HasIndex(r => new { r.Slug, r.Region, r.BranchId })
            .IsUnique();

        modelBuilder.Entity<Realm>()
            .HasOne(r => r.Branch)
            .WithMany()
            .HasForeignKey(r => r.BranchId);

        // Branch → Expansion (no back-nav on Expansion)
        modelBuilder.Entity<Branch>()
            .HasOne(b => b.CurrentExpansion)
            .WithMany()
            .HasForeignKey(b => b.CurrentExpansionId);

        // Spec → WowClass
        modelBuilder.Entity<Spec>()
            .HasOne(s => s.Class)
            .WithMany(c => c.Specs)
            .HasForeignKey(s => s.ClassId);
    }

    // ── Static seed data ──────────────────────────────────────────────────

    private static void SeedStaticData(ModelBuilder modelBuilder)
    {
        SeedExpansions(modelBuilder);
        SeedBranches(modelBuilder);
        SeedRaces(modelBuilder);
        SeedClasses(modelBuilder);
        SeedSpecs(modelBuilder);
    }

    private static void SeedExpansions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Expansion>().HasData(
            new Expansion { Id = 1,  Name = "Classic",                ShortCode = "Classic", ReleaseOrder = 1  },
            new Expansion { Id = 2,  Name = "The Burning Crusade",    ShortCode = "TBC",     ReleaseOrder = 2  },
            new Expansion { Id = 3,  Name = "Wrath of the Lich King", ShortCode = "WotLK",   ReleaseOrder = 3  },
            new Expansion { Id = 4,  Name = "Cataclysm",              ShortCode = "Cata",    ReleaseOrder = 4  },
            new Expansion { Id = 5,  Name = "Mists of Pandaria",      ShortCode = "MoP",     ReleaseOrder = 5  },
            new Expansion { Id = 6,  Name = "Warlords of Draenor",    ShortCode = "WoD",     ReleaseOrder = 6  },
            new Expansion { Id = 7,  Name = "Legion",                 ShortCode = "Legion",  ReleaseOrder = 7  },
            new Expansion { Id = 8,  Name = "Battle for Azeroth",     ShortCode = "BfA",     ReleaseOrder = 8  },
            new Expansion { Id = 9,  Name = "Shadowlands",            ShortCode = "SL",      ReleaseOrder = 9  },
            new Expansion { Id = 10, Name = "Dragonflight",           ShortCode = "DF",      ReleaseOrder = 10 },
            new Expansion { Id = 11, Name = "The War Within",         ShortCode = "TWW",     ReleaseOrder = 11 }
        );
    }

    private static void SeedBranches(ModelBuilder modelBuilder)
    {
        // BnetNamespacePrefix: append "-{region}" at query time to get the full namespace.
        // e.g. "dynamic-classic1x" + "-eu" → "dynamic-classic1x-eu"
        modelBuilder.Entity<Branch>().HasData(
            new Branch { Id = 1, Name = "Retail",              BnetNamespacePrefix = "dynamic",            CurrentExpansionId = 11 },
            new Branch { Id = 2, Name = "Classic Era",         BnetNamespacePrefix = "dynamic-classic1x",  CurrentExpansionId = 1  },
            new Branch { Id = 3, Name = "Classic",             BnetNamespacePrefix = "dynamic-classic",    CurrentExpansionId = 5  },
            new Branch { Id = 4, Name = "Classic Anniversary", BnetNamespacePrefix = "dynamic-classicann", CurrentExpansionId = 2  }
        );
    }

    private static void SeedRaces(ModelBuilder modelBuilder)
    {
        // IDs match Blizzard's playable race IDs from the BNet character profile API.
        // Dracthyr (52/70) and Earthen (84/85) IDs are best-effort — verify against live API if needed.
        modelBuilder.Entity<Race>().HasData(
            // ── Classic ───────────────────────────────────────────────────
            new Race { Id = 1,  Name = "Human",               Faction = Faction.Alliance, FirstExpansionId = 1  },
            new Race { Id = 2,  Name = "Orc",                 Faction = Faction.Horde,    FirstExpansionId = 1  },
            new Race { Id = 3,  Name = "Dwarf",               Faction = Faction.Alliance, FirstExpansionId = 1  },
            new Race { Id = 4,  Name = "Night Elf",           Faction = Faction.Alliance, FirstExpansionId = 1  },
            new Race { Id = 5,  Name = "Undead",              Faction = Faction.Horde,    FirstExpansionId = 1  },
            new Race { Id = 6,  Name = "Tauren",              Faction = Faction.Horde,    FirstExpansionId = 1  },
            new Race { Id = 7,  Name = "Gnome",               Faction = Faction.Alliance, FirstExpansionId = 1  },
            new Race { Id = 8,  Name = "Troll",               Faction = Faction.Horde,    FirstExpansionId = 1  },
            // ── TBC ───────────────────────────────────────────────────────
            new Race { Id = 10, Name = "Blood Elf",           Faction = Faction.Horde,    FirstExpansionId = 2  },
            new Race { Id = 11, Name = "Draenei",             Faction = Faction.Alliance, FirstExpansionId = 2  },
            // ── Cataclysm ─────────────────────────────────────────────────
            new Race { Id = 9,  Name = "Goblin",              Faction = Faction.Horde,    FirstExpansionId = 4  },
            new Race { Id = 22, Name = "Worgen",              Faction = Faction.Alliance, FirstExpansionId = 4  },
            // ── Mists of Pandaria ─────────────────────────────────────────
            // Blizzard uses three IDs for Pandaren: 24 = neutral (faction not yet chosen),
            // 25 = Alliance, 26 = Horde.
            new Race { Id = 24, Name = "Pandaren",            Faction = Faction.Neutral,  FirstExpansionId = 5  },
            new Race { Id = 25, Name = "Pandaren",            Faction = Faction.Alliance, FirstExpansionId = 5  },
            new Race { Id = 26, Name = "Pandaren",            Faction = Faction.Horde,    FirstExpansionId = 5  },
            // ── BfA allied races ──────────────────────────────────────────
            new Race { Id = 27, Name = "Nightborne",          Faction = Faction.Horde,    FirstExpansionId = 8  },
            new Race { Id = 28, Name = "Highmountain Tauren", Faction = Faction.Horde,    FirstExpansionId = 8  },
            new Race { Id = 29, Name = "Void Elf",            Faction = Faction.Alliance, FirstExpansionId = 8  },
            new Race { Id = 30, Name = "Lightforged Draenei", Faction = Faction.Alliance, FirstExpansionId = 8  },
            new Race { Id = 31, Name = "Zandalari Troll",     Faction = Faction.Horde,    FirstExpansionId = 8  },
            new Race { Id = 32, Name = "Mag'har Orc",         Faction = Faction.Horde,    FirstExpansionId = 8  },
            new Race { Id = 34, Name = "Dark Iron Dwarf",     Faction = Faction.Alliance, FirstExpansionId = 8  },
            new Race { Id = 35, Name = "Vulpera",             Faction = Faction.Horde,    FirstExpansionId = 8  },
            new Race { Id = 36, Name = "Kul Tiran",           Faction = Faction.Alliance, FirstExpansionId = 8  },
            new Race { Id = 37, Name = "Mechagnome",          Faction = Faction.Alliance, FirstExpansionId = 8  },
            // ── Dragonflight ──────────────────────────────────────────────
            new Race { Id = 52, Name = "Dracthyr (Alliance)", Faction = Faction.Alliance, FirstExpansionId = 10 },
            new Race { Id = 70, Name = "Dracthyr (Horde)",    Faction = Faction.Horde,    FirstExpansionId = 10 },
            // ── The War Within ────────────────────────────────────────────
            new Race { Id = 84, Name = "Earthen (Alliance)",  Faction = Faction.Alliance, FirstExpansionId = 11 },
            new Race { Id = 85, Name = "Earthen (Horde)",     Faction = Faction.Horde,    FirstExpansionId = 11 }
        );
    }

    private static void SeedClasses(ModelBuilder modelBuilder)
    {
        // IDs match Blizzard's character_class.id from the BNet character profile API.
        // Colors are the official class hex colours (no leading #).
        modelBuilder.Entity<WowClass>().HasData(
            new WowClass { Id = 1,  Name = "Warrior",      Color = "C79C6E", FirstExpansionId = 1  },
            new WowClass { Id = 2,  Name = "Paladin",      Color = "F58CBA", FirstExpansionId = 1  },
            new WowClass { Id = 3,  Name = "Hunter",       Color = "ABD473", FirstExpansionId = 1  },
            new WowClass { Id = 4,  Name = "Rogue",        Color = "FFF569", FirstExpansionId = 1  },
            new WowClass { Id = 5,  Name = "Priest",       Color = "FFFFFF", FirstExpansionId = 1  },
            new WowClass { Id = 6,  Name = "Death Knight", Color = "C41F3B", FirstExpansionId = 3  },
            new WowClass { Id = 7,  Name = "Shaman",       Color = "0070DE", FirstExpansionId = 1  },
            new WowClass { Id = 8,  Name = "Mage",         Color = "69CCF0", FirstExpansionId = 1  },
            new WowClass { Id = 9,  Name = "Warlock",      Color = "9482C9", FirstExpansionId = 1  },
            new WowClass { Id = 10, Name = "Monk",         Color = "00FF96", FirstExpansionId = 5  },
            new WowClass { Id = 11, Name = "Druid",        Color = "FF7D0A", FirstExpansionId = 1  },
            new WowClass { Id = 12, Name = "Demon Hunter", Color = "A330C9", FirstExpansionId = 7  },
            new WowClass { Id = 13, Name = "Evoker",       Color = "33937F", FirstExpansionId = 10 }
        );
    }

    private static void SeedSpecs(ModelBuilder modelBuilder)
    {
        // IDs match Blizzard's active_spec.id from the BNet character profile API.
        const string iconBase = "https://render.worldofwarcraft.com/eu/icons/56/";

        modelBuilder.Entity<Spec>().HasData(
            // ── Warrior ───────────────────────────────────────────────────
            new Spec { Id = 71,   Name = "Arms",          Role = SpecRole.Dps,    ClassId = 1,  FirstExpansionId = 1,  IconUrl = iconBase + "ability_warrior_savageblow.jpg" },
            new Spec { Id = 72,   Name = "Fury",          Role = SpecRole.Dps,    ClassId = 1,  FirstExpansionId = 1,  IconUrl = iconBase + "ability_warrior_innerrage.jpg" },
            new Spec { Id = 73,   Name = "Protection",    Role = SpecRole.Tank,   ClassId = 1,  FirstExpansionId = 1,  IconUrl = iconBase + "ability_warrior_defensivestance.jpg" },
            // ── Paladin ───────────────────────────────────────────────────
            new Spec { Id = 65,   Name = "Holy",          Role = SpecRole.Healer, ClassId = 2,  FirstExpansionId = 1,  IconUrl = iconBase + "spell_holy_holybolt.jpg" },
            new Spec { Id = 66,   Name = "Protection",    Role = SpecRole.Tank,   ClassId = 2,  FirstExpansionId = 1,  IconUrl = iconBase + "ability_paladin_shieldofthetemplar.jpg" },
            new Spec { Id = 70,   Name = "Retribution",   Role = SpecRole.Dps,    ClassId = 2,  FirstExpansionId = 1,  IconUrl = iconBase + "spell_holy_auraoflight.jpg" },
            // ── Hunter ────────────────────────────────────────────────────
            new Spec { Id = 253,  Name = "Beast Mastery", Role = SpecRole.Dps,    ClassId = 3,  FirstExpansionId = 1,  IconUrl = iconBase + "ability_hunter_bestialdiscipline.jpg" },
            new Spec { Id = 254,  Name = "Marksmanship",  Role = SpecRole.Dps,    ClassId = 3,  FirstExpansionId = 1,  IconUrl = iconBase + "ability_hunter_focusedaim.jpg" },
            new Spec { Id = 255,  Name = "Survival",      Role = SpecRole.Dps,    ClassId = 3,  FirstExpansionId = 1,  IconUrl = iconBase + "ability_hunter_camouflage.jpg" },
            // ── Rogue ─────────────────────────────────────────────────────
            new Spec { Id = 259,  Name = "Assassination", Role = SpecRole.Dps,    ClassId = 4,  FirstExpansionId = 1,  IconUrl = iconBase + "ability_rogue_deadlybrew.jpg" },
            new Spec { Id = 260,  Name = "Outlaw",        Role = SpecRole.Dps,    ClassId = 4,  FirstExpansionId = 1,  IconUrl = iconBase + "ability_rogue_waylay.jpg" },
            new Spec { Id = 261,  Name = "Subtlety",      Role = SpecRole.Dps,    ClassId = 4,  FirstExpansionId = 1,  IconUrl = iconBase + "ability_stealth.jpg" },
            // ── Priest ────────────────────────────────────────────────────
            new Spec { Id = 256,  Name = "Discipline",    Role = SpecRole.Healer, ClassId = 5,  FirstExpansionId = 1,  IconUrl = iconBase + "spell_holy_powerwordshield.jpg" },
            new Spec { Id = 257,  Name = "Holy",          Role = SpecRole.Healer, ClassId = 5,  FirstExpansionId = 1,  IconUrl = iconBase + "spell_holy_guardianspirit.jpg" },
            new Spec { Id = 258,  Name = "Shadow",        Role = SpecRole.Dps,    ClassId = 5,  FirstExpansionId = 1,  IconUrl = iconBase + "spell_shadow_shadowwordpain.jpg" },
            // ── Death Knight ──────────────────────────────────────────────
            new Spec { Id = 250,  Name = "Blood",         Role = SpecRole.Tank,   ClassId = 6,  FirstExpansionId = 3,  IconUrl = iconBase + "spell_deathknight_bloodpresence.jpg" },
            new Spec { Id = 251,  Name = "Frost",         Role = SpecRole.Dps,    ClassId = 6,  FirstExpansionId = 3,  IconUrl = iconBase + "spell_deathknight_frostpresence.jpg" },
            new Spec { Id = 252,  Name = "Unholy",        Role = SpecRole.Dps,    ClassId = 6,  FirstExpansionId = 3,  IconUrl = iconBase + "spell_deathknight_unholypresence.jpg" },
            // ── Shaman ────────────────────────────────────────────────────
            new Spec { Id = 262,  Name = "Elemental",     Role = SpecRole.Dps,    ClassId = 7,  FirstExpansionId = 1,  IconUrl = iconBase + "spell_nature_lightning.jpg" },
            new Spec { Id = 263,  Name = "Enhancement",   Role = SpecRole.Dps,    ClassId = 7,  FirstExpansionId = 1,  IconUrl = iconBase + "spell_shaman_improvedstormstrike.jpg" },
            new Spec { Id = 264,  Name = "Restoration",   Role = SpecRole.Healer, ClassId = 7,  FirstExpansionId = 1,  IconUrl = iconBase + "spell_nature_magicimmunity.jpg" },
            // ── Mage ──────────────────────────────────────────────────────
            new Spec { Id = 62,   Name = "Arcane",        Role = SpecRole.Dps,    ClassId = 8,  FirstExpansionId = 1,  IconUrl = iconBase + "spell_holy_magicalsentry.jpg" },
            new Spec { Id = 63,   Name = "Fire",          Role = SpecRole.Dps,    ClassId = 8,  FirstExpansionId = 1,  IconUrl = iconBase + "spell_fire_firebolt02.jpg" },
            new Spec { Id = 64,   Name = "Frost",         Role = SpecRole.Dps,    ClassId = 8,  FirstExpansionId = 1,  IconUrl = iconBase + "spell_frost_frostbolt02.jpg" },
            // ── Warlock ───────────────────────────────────────────────────
            new Spec { Id = 265,  Name = "Affliction",    Role = SpecRole.Dps,    ClassId = 9,  FirstExpansionId = 1,  IconUrl = iconBase + "spell_shadow_deathcoil.jpg" },
            new Spec { Id = 266,  Name = "Demonology",    Role = SpecRole.Dps,    ClassId = 9,  FirstExpansionId = 1,  IconUrl = iconBase + "spell_shadow_metamorphosis.jpg" },
            new Spec { Id = 267,  Name = "Destruction",   Role = SpecRole.Dps,    ClassId = 9,  FirstExpansionId = 1,  IconUrl = iconBase + "spell_shadow_rainoffire.jpg" },
            // ── Monk ──────────────────────────────────────────────────────
            new Spec { Id = 268,  Name = "Brewmaster",    Role = SpecRole.Tank,   ClassId = 10, FirstExpansionId = 5,  IconUrl = iconBase + "spell_monk_brewmaster_spec.jpg" },
            new Spec { Id = 269,  Name = "Windwalker",    Role = SpecRole.Dps,    ClassId = 10, FirstExpansionId = 5,  IconUrl = iconBase + "spell_monk_windwalker_spec.jpg" },
            new Spec { Id = 270,  Name = "Mistweaver",    Role = SpecRole.Healer, ClassId = 10, FirstExpansionId = 5,  IconUrl = iconBase + "spell_monk_mistweaver_spec.jpg" },
            // ── Druid ─────────────────────────────────────────────────────
            new Spec { Id = 102,  Name = "Balance",       Role = SpecRole.Dps,    ClassId = 11, FirstExpansionId = 1,  IconUrl = iconBase + "spell_nature_starfall.jpg" },
            new Spec { Id = 103,  Name = "Feral",         Role = SpecRole.Dps,    ClassId = 11, FirstExpansionId = 1,  IconUrl = iconBase + "ability_druid_catform.jpg" },
            new Spec { Id = 104,  Name = "Guardian",      Role = SpecRole.Tank,   ClassId = 11, FirstExpansionId = 5,  IconUrl = iconBase + "ability_racial_bearform.jpg" },
            new Spec { Id = 105,  Name = "Restoration",   Role = SpecRole.Healer, ClassId = 11, FirstExpansionId = 1,  IconUrl = iconBase + "spell_nature_healingtouch.jpg" },
            // ── Demon Hunter ──────────────────────────────────────────────
            new Spec { Id = 577,  Name = "Havoc",         Role = SpecRole.Dps,    ClassId = 12, FirstExpansionId = 7,  IconUrl = iconBase + "ability_demonhunter_specdps.jpg" },
            new Spec { Id = 581,  Name = "Vengeance",     Role = SpecRole.Tank,   ClassId = 12, FirstExpansionId = 7,  IconUrl = iconBase + "ability_demonhunter_spectank.jpg" },
            // ── Evoker ────────────────────────────────────────────────────
            new Spec { Id = 1467, Name = "Devastation",   Role = SpecRole.Dps,    ClassId = 13, FirstExpansionId = 10, IconUrl = iconBase + "classicon_evoker_devastation.jpg" },
            new Spec { Id = 1468, Name = "Preservation",  Role = SpecRole.Healer, ClassId = 13, FirstExpansionId = 10, IconUrl = iconBase + "classicon_evoker_preservation.jpg" },
            new Spec { Id = 1473, Name = "Augmentation",  Role = SpecRole.Dps,    ClassId = 13, FirstExpansionId = 10, IconUrl = iconBase + "classicon_evoker_augmentation.jpg" }
        );
    }
}
