using Microsoft.EntityFrameworkCore;
using Eat_Experience.Models;
using System.Collections.Generic;

namespace Eat_Experience.Data
{
    public class AppDbContext :DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<ProductoExtra> ProductoExtras { get; set; }

        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<DetallePedido> DetallesPedido { get; set; }
        public DbSet<Administrador> Administradores { get; set; }
        public DbSet<DetallePedidoExtra> DetallePedidoExtras { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---------------- Defaults en DB (UTC) ----------------
            modelBuilder.Entity<Administrador>()
                .Property(a => a.FechaRegistro)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Pedido>()
                .Property(p => p.Fecha)
                .HasDefaultValueSql("GETUTCDATE()");

            // ---------------- Relaciones + DeleteBehavior ----------------
            // Categoria (1) - (N) Producto
            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Categoria)
                .WithMany(c => c.Productos)
                .HasForeignKey(p => p.CategoriaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Producto (1) - (N) ProductoExtra
            modelBuilder.Entity<ProductoExtra>()
                .HasOne(e => e.Producto)
                .WithMany(p => p.Extras)
                .HasForeignKey(e => e.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            // Administrador (1) - (N) Pedido
            modelBuilder.Entity<Pedido>()
                .HasOne(p => p.Administrador)
                .WithMany() // si no mantenés la colección en Administrador
                .HasForeignKey(p => p.AdministradorId)
                .OnDelete(DeleteBehavior.Restrict);

            // (MT) Administrador (1) - (N) Categoria
            modelBuilder.Entity<Categoria>()
                .HasOne(c => c.Administrador)
                .WithMany()
                .HasForeignKey(c => c.AdministradorId)
                .OnDelete(DeleteBehavior.Restrict);

            // (MT) Administrador (1) - (N) Producto
            modelBuilder.Entity<Producto>()
                .HasOne(p => p.Administrador)
                .WithMany()
                .HasForeignKey(p => p.AdministradorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Pedido (1) - (N) DetallePedido
            modelBuilder.Entity<DetallePedido>()
                .HasOne(d => d.Pedido)
                .WithMany(p => p.Detalles)
                .HasForeignKey(d => d.PedidoId)
                .OnDelete(DeleteBehavior.Cascade);
            // (Queremos que al borrar un Pedido caigan sus Detalles)

            // DetallePedido (N) - (1) Producto
            modelBuilder.Entity<DetallePedido>()
                .HasOne(d => d.Producto)
                .WithMany()
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);
            // (Nunca borrar Productos “históricos” por tener Detalles)

            // DetallePedido (1) - (N) DetallePedidoExtra
            modelBuilder.Entity<DetallePedidoExtra>()
                .HasOne(dpe => dpe.DetallePedido)
                .WithMany(dp => dp.ProductosExtra)
                .HasForeignKey(dpe => dpe.DetallePedidoId)
                .OnDelete(DeleteBehavior.Cascade);
            // (Si se borra el Detalle, se borran sus Extras)

            // DetallePedidoExtra (N) - (1) ProductoExtra
            modelBuilder.Entity<DetallePedidoExtra>()
                .HasOne(dpe => dpe.ProductoExtra)
                .WithMany()
                .HasForeignKey(dpe => dpe.ProductoExtraId)
                .OnDelete(DeleteBehavior.Restrict);
            // (No borres un Extra si hay historial)

            // ---------------- Precisión de dinero (por seguridad) ----------------
            modelBuilder.Entity<Producto>().Property(p => p.Precio).HasPrecision(18, 2);
            modelBuilder.Entity<ProductoExtra>().Property(e => e.PrecioAdicional).HasPrecision(18, 2);
            modelBuilder.Entity<DetallePedido>().Property(d => d.PrecioUnitario).HasPrecision(18, 2);
            modelBuilder.Entity<Pedido>().Property(p => p.Total).HasPrecision(18, 2);
            modelBuilder.Entity<Pedido>().Property(p => p.MontoPagoEfectivo).HasPrecision(18, 2);

            // ---------------- Índices / Unicidades ----------------
            modelBuilder.Entity<Administrador>()
                .HasIndex(a => a.Email)
                .IsUnique();

            // (MT) Evitar duplicados por tenant
            modelBuilder.Entity<Categoria>()
                .HasIndex(c => new { c.AdministradorId, c.Nombre })
                .IsUnique();

            modelBuilder.Entity<Producto>()
                .HasIndex(p => new { p.AdministradorId, p.Nombre })
                .IsUnique();

            modelBuilder.Entity<Pedido>()
                .HasIndex(p => new { p.AdministradorId, p.Fecha });

            // Evita repetir el mismo extra dos veces en el mismo ítem
            modelBuilder.Entity<DetallePedidoExtra>()
                .HasIndex(x => new { x.DetallePedidoId, x.ProductoExtraId })
                .IsUnique();

            // ---------------- (Opcional) Filtros globales multi-tenant ----------------
            // Si inyectás ITenantProvider en el DbContext (constructor), activá:
            // modelBuilder.Entity<Categoria>().HasQueryFilter(c => c.AdministradorId == _tenant.AdministradorId);
            // modelBuilder.Entity<Producto>().HasQueryFilter(p => p.AdministradorId == _tenant.AdministradorId);
            // modelBuilder.Entity<Pedido>().HasQueryFilter(p => p.AdministradorId == _tenant.AdministradorId);
        }

    }




}
