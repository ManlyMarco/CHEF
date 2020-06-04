﻿// <auto-generated />
using CHEF.Components.Commands.Cooking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace CHEF.Migrations
{
    [DbContext(typeof(RecipeContext))]
    partial class RecipeContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .HasAnnotation("ProductVersion", "3.1.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            modelBuilder.Entity("CHEF.Components.Commands.Cooking.Recipe", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnName("id")
                        .HasColumnType("integer")
                        .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

                    b.Property<string>("Name")
                        .HasColumnName("name")
                        .HasColumnType("text");

                    b.Property<decimal>("OwnerId")
                        .HasColumnName("owner_id")
                        .HasColumnType("numeric(20,0)");

                    b.Property<string>("OwnerName")
                        .HasColumnName("owner_name")
                        .HasColumnType("text");

                    b.Property<string>("Text")
                        .HasColumnName("text")
                        .HasColumnType("text");

                    b.HasKey("Id")
                        .HasName("pk_recipes");

                    b.ToTable("recipes");
                });
#pragma warning restore 612, 618
        }
    }
}