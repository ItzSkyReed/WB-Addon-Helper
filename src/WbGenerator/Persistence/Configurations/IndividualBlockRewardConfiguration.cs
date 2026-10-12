using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WbGenerator.Enitites;

namespace WbGenerator.Persistence.Configurations;


public class IndividualBlockRewardConfiguration : IEntityTypeConfiguration<IndividualBlockReward>
{
    public void Configure(EntityTypeBuilder<IndividualBlockReward> builder)
    {
        builder.ToTable("individual_blocks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).IsRequired().HasColumnName("id");
        builder.Property(x => x.Path).IsRequired().HasColumnName("path");
        builder.Property(x => x.CommandType).IsRequired().HasColumnName("command_type");
        builder.Property(x => x.BlocksBacap).IsRequired(false).HasColumnName("blocks_bacap");
        builder.Property(x => x.BlocksBacaped).IsRequired(false).HasColumnName("blocks_bacaped");

        builder.HasIndex(x => x.Path)
            .IsUnique();
    }
}