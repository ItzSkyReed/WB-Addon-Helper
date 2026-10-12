using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WbGenerator.Enitites;

namespace WbGenerator.Persistence.Configurations;


public class AdvancementTierOverrideConfiguration : IEntityTypeConfiguration<AdvancementTierOverride>
{
    public void Configure(EntityTypeBuilder<AdvancementTierOverride> builder)
    {
        builder.ToTable("tier_overrides");

        builder.HasKey(x => x.McPath);

        builder.Property(x => x.McPath).IsRequired().HasColumnName("mc_path");
        builder.Property(x => x.Bacap).IsRequired(false).HasColumnName("bacap");
        builder.Property(x => x.Bacaped).IsRequired(false).HasColumnName("bacaped");
    }
}