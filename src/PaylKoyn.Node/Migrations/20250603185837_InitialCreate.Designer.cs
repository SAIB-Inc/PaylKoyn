﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PaylKoyn.Node.Data;

#nullable disable

namespace PaylKoyn.Node.Migrations
{
    [DbContext(typeof(WalletDbContext))]
    [Migration("20250603185837_InitialCreate")]
    partial class InitialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "9.0.5");

            modelBuilder.Entity("PaylKoyn.Data.Models.Wallet", b =>
                {
                    b.Property<string>("Address")
                        .HasColumnType("TEXT");

                    b.Property<int>("Index")
                        .HasColumnType("INTEGER");

                    b.HasKey("Address");

                    b.ToTable("Wallets");
                });
#pragma warning restore 612, 618
        }
    }
}
