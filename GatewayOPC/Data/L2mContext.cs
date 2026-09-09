using Microsoft.EntityFrameworkCore;
using GatewayOPC.Models;

namespace GatewayOPC.Data
{
    public class L2mContext : DbContext
    {
        public L2mContext()
        {
        }

        public L2mContext(DbContextOptions<L2mContext> options) : base(options)
        {
        }

        public DbSet<Anemometro> Anemometros { get; set; } = null!;
        public DbSet<AnemometroHistorico> Anemometro_historicos { get; set; } = null!;
        public DbSet<Gateway> Gateways { get; set; } = null!;
        public DbSet<Tracker> Trackers { get; set; } = null!;
        public DbSet<TrackerHistorico> Tracker_historicos { get; set; } = null!;
        public DbSet<Subcampo> Subcampos { get; set; } = null!;
        public DbSet<SubcampoPontos> SubcampoPontos { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<AnemometroHistorico>().HasKey(table => new {
                table.id, table.leitura
            });

            builder.Entity<TrackerHistorico>().HasKey(table => new {
                table.id, table.leitura
            });

            builder.Entity<SubcampoPontos>().HasKey(table => new {
                table.id_ponto,
                table.id_subcampo
            });

            builder.Entity<Anemometro>()
                .HasOne(i => i.Subcampo)
                .WithMany(i => i.Anemometros)
                .HasForeignKey(i => i.id_subcampo)
                .HasPrincipalKey(i => i.id);

            builder.Entity<Tracker>()
                .HasOne(i => i.Subcampo)
                .WithMany(i => i.Trackers)
                .HasForeignKey(i => i.id_subcampo)
                .HasPrincipalKey(i => i.id);

            builder.Entity<Gateway>()
                .HasOne(i => i.Subcampo)
                .WithMany(i => i.Gateways)
                .HasForeignKey(i => i.id_subcampo)
                .HasPrincipalKey(i => i.id);

            builder.Entity<AnemometroHistorico>()
                .HasOne(i => i.anemometro)
                .WithMany(i => i.Anemometro_historicos)
                .HasForeignKey(i => i.id)
                .HasPrincipalKey(i => i.id);

            builder.Entity<Tracker>()
                .HasOne(i => i.gateway)
                .WithMany(i => i.Trackers)
                .HasForeignKey(i => i.gateway_id)
                .HasPrincipalKey(i => i.id);

            builder.Entity<SubcampoPontos>()
                .HasOne(i => i.Subcampo)
                .WithMany(i => i.Subcampo_pontos)
                .HasForeignKey(i => i.id_subcampo)
                .HasPrincipalKey(i => i.id);

            builder.Entity<TrackerHistorico>()
                .HasOne(i => i.tracker)
                .WithMany(i => i.Tracker_historicos)
                .HasForeignKey(i => i.id)
                .HasPrincipalKey(i => i.id);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Conventions.Add(_ => new BlankTriggerAddingConvention());
        }
    }
}
