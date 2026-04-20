using Microsoft.EntityFrameworkCore;
using Vinto.Api.Models;
using System.Collections.Generic;

namespace Vinto.Api.Data
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
        public DbSet<ComentarioPedido> ComentariosPedido { get; set; }
        public DbSet<Imagen> Imagenes { get; set; }
        public DbSet<TipoVariante> TiposVariante { get; set; }
        public DbSet<OpcionVariante> OpcionesVariante { get; set; }
        public DbSet<VarianteProducto> VariantesProducto { get; set; }
        public DbSet<MovimientoStock> MovimientosStock { get; set; }

        public DbSet<Descuento> Descuentos { get; set; }
        public DbSet<Cupon> Cupones { get; set; }
        public DbSet<UsoCupon> UsosCupones { get; set; }
        public DbSet<DetallePedidoDescuento> DetallePedidoDescuentos { get; set; }
        public DbSet<PreviewActualizacionPrecios> PreviewsActualizacionPrecios { get; set; }
        public DbSet<PreviewActualizacionPreciosItem> PreviewActualizacionPreciosItems { get; set; }

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

            // Pedido (1) - (N) ComentarioPedido
            modelBuilder.Entity<ComentarioPedido>()
                .HasOne(c => c.Pedido)
                .WithMany()
                .HasForeignKey(c => c.PedidoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ComentarioPedido>()
                .HasOne(c => c.Administrador)
                .WithMany()
                .HasForeignKey(c => c.AdministradorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComentarioPedido>()
                .HasIndex(c => c.PedidoId);

            // ---------------- Imagen ----------------
            modelBuilder.Entity<Imagen>()
                .Property(i => i.NombreOriginal).HasMaxLength(255);

            modelBuilder.Entity<Imagen>()
                .Property(i => i.NombreAlmacenado).HasMaxLength(255);

            modelBuilder.Entity<Imagen>()
                .Property(i => i.ContentType).HasMaxLength(100);

            modelBuilder.Entity<Imagen>()
                .Property(i => i.Url).HasMaxLength(500);

            modelBuilder.Entity<Imagen>()
                .Property(i => i.Tipo).HasMaxLength(50);

            modelBuilder.Entity<Imagen>()
                .Property(i => i.Orden).HasDefaultValue(0);

            modelBuilder.Entity<Imagen>()
                .Property(i => i.FechaCreacion).HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Imagen>()
                .HasOne(i => i.Administrador)
                .WithMany()
                .HasForeignKey(i => i.AdministradorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Imagen>()
                .HasIndex(i => new { i.AdministradorId, i.Tipo, i.EntidadId });

            // ---------------- Variantes ----------------

            // Producto (1) - (N) TipoVariante
            modelBuilder.Entity<TipoVariante>()
                .HasOne(t => t.Producto)
                .WithMany(p => p.TiposVariante)
                .HasForeignKey(t => t.ProductoId)
                .OnDelete(DeleteBehavior.Cascade);

            // TipoVariante (1) - (N) OpcionVariante
            modelBuilder.Entity<OpcionVariante>()
                .HasOne(o => o.TipoVariante)
                .WithMany(t => t.Opciones)
                .HasForeignKey(o => o.TipoVarianteId)
                .OnDelete(DeleteBehavior.Cascade);

            // Producto (1) - (N) VarianteProducto
            modelBuilder.Entity<VarianteProducto>()
                .HasOne(v => v.Producto)
                .WithMany(p => p.Variantes)
                .HasForeignKey(v => v.ProductoId)
                .OnDelete(DeleteBehavior.Cascade);

            // OpcionVariante (1) - (N) VarianteProducto via Opcion1
            modelBuilder.Entity<VarianteProducto>()
                .HasOne(v => v.Opcion1)
                .WithMany()
                .HasForeignKey(v => v.Opcion1Id)
                .OnDelete(DeleteBehavior.Restrict);

            // OpcionVariante (1) - (N) VarianteProducto via Opcion2 (nullable)
            modelBuilder.Entity<VarianteProducto>()
                .HasOne(v => v.Opcion2)
                .WithMany()
                .HasForeignKey(v => v.Opcion2Id)
                .OnDelete(DeleteBehavior.Restrict);

            // VarianteProducto (1) - (N) DetallePedido
            modelBuilder.Entity<DetallePedido>()
                .HasOne(d => d.VarianteProducto)
                .WithMany()
                .HasForeignKey(d => d.VarianteProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------------- MovimientoStock ----------------

            modelBuilder.Entity<MovimientoStock>()
                .HasOne(m => m.Administrador)
                .WithMany()
                .HasForeignKey(m => m.AdministradorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MovimientoStock>()
                .HasOne(m => m.Producto)
                .WithMany()
                .HasForeignKey(m => m.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MovimientoStock>()
                .HasOne(m => m.VarianteProducto)
                .WithMany()
                .HasForeignKey(m => m.VarianteProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MovimientoStock>()
                .Property(m => m.FechaCreacion)
                .HasDefaultValueSql("GETUTCDATE()");

            // Precisión monetaria
            modelBuilder.Entity<VarianteProducto>().Property(v => v.Precio).HasPrecision(18, 2);

            // ---------------- (Opcional) Filtros globales multi-tenant ----------------
            // Si inyectás ITenantProvider en el DbContext (constructor), activá:
            // modelBuilder.Entity<Categoria>().HasQueryFilter(c => c.AdministradorId == _tenant.AdministradorId);
            // modelBuilder.Entity<Producto>().HasQueryFilter(p => p.AdministradorId == _tenant.AdministradorId);
            // modelBuilder.Entity<Pedido>().HasQueryFilter(p => p.AdministradorId == _tenant.AdministradorId);

            // ---------------- Descuento ----------------
            modelBuilder.Entity<Descuento>()
                .HasOne(d => d.Administrador)
                .WithMany()
                .HasForeignKey(d => d.AdministradorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Descuento>()
                .HasOne(d => d.Producto)
                .WithMany()
                .HasForeignKey(d => d.ProductoId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Descuento>()
                .HasOne(d => d.Categoria)
                .WithMany()
                .HasForeignKey(d => d.CategoriaId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Descuento>()
                .Property(d => d.Valor).HasPrecision(18, 2);

            modelBuilder.Entity<Descuento>()
                .Property(d => d.FechaCreacion).HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Descuento>()
                .HasIndex(d => new { d.AdministradorId, d.Activo });

            // ---------------- Cupon ----------------
            modelBuilder.Entity<Cupon>()
                .HasOne(c => c.Administrador)
                .WithMany()
                .HasForeignKey(c => c.AdministradorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Cupon>()
                .Property(c => c.Valor).HasPrecision(18, 2);

            modelBuilder.Entity<Cupon>()
                .Property(c => c.PedidoMinimo).HasPrecision(18, 2);

            modelBuilder.Entity<Cupon>()
                .Property(c => c.FechaCreacion).HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Cupon>()
                .HasIndex(c => new { c.AdministradorId, c.Codigo })
                .IsUnique();

            // ---------------- Pedido → Cupon ----------------
            modelBuilder.Entity<Pedido>()
                .HasOne(p => p.Cupon)
                .WithMany()
                .HasForeignKey(p => p.CuponId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Pedido>()
                .Property(p => p.SubtotalSinDescuentos).HasPrecision(18, 2).HasDefaultValue(0m);

            modelBuilder.Entity<Pedido>()
                .Property(p => p.MontoDescuentoProductos).HasPrecision(18, 2).HasDefaultValue(0m);

            modelBuilder.Entity<Pedido>()
                .Property(p => p.MontoDescuentoCupon).HasPrecision(18, 2).HasDefaultValue(0m);

            // ---------------- UsoCupon ----------------
            modelBuilder.Entity<UsoCupon>()
                .HasOne(u => u.Cupon)
                .WithMany()
                .HasForeignKey(u => u.CuponId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UsoCupon>()
                .HasOne(u => u.Pedido)
                .WithMany()
                .HasForeignKey(u => u.PedidoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UsoCupon>()
                .Property(u => u.MontoDescontado).HasPrecision(18, 2);

            modelBuilder.Entity<UsoCupon>()
                .Property(u => u.FechaUso).HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<UsoCupon>()
                .HasIndex(u => u.CuponId);

            modelBuilder.Entity<UsoCupon>()
                .HasIndex(u => u.PedidoId);

            // ---------------- DetallePedidoDescuento ----------------
            modelBuilder.Entity<DetallePedidoDescuento>()
                .HasOne(d => d.Pedido)
                .WithMany()
                .HasForeignKey(d => d.PedidoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DetallePedidoDescuento>()
                .HasOne(d => d.DetallePedido)
                .WithMany()
                .HasForeignKey(d => d.DetallePedidoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DetallePedidoDescuento>()
                .HasOne(d => d.Descuento)
                .WithMany()
                .HasForeignKey(d => d.DescuentoId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<DetallePedidoDescuento>()
                .Property(d => d.MontoDescontado).HasPrecision(18, 2);

            modelBuilder.Entity<DetallePedidoDescuento>()
                .Property(d => d.FechaCreacion).HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<DetallePedidoDescuento>()
                .HasIndex(d => d.PedidoId);

            modelBuilder.Entity<DetallePedidoDescuento>()
                .HasIndex(d => d.DetallePedidoId);

            // ---------------- PreviewActualizacionPrecios ----------------
            modelBuilder.Entity<PreviewActualizacionPrecios>()
                .HasOne(p => p.Administrador)
                .WithMany()
                .HasForeignKey(p => p.AdministradorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PreviewActualizacionPrecios>()
                .HasOne(p => p.Categoria)
                .WithMany()
                .HasForeignKey(p => p.CategoriaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PreviewActualizacionPrecios>()
                .Property(p => p.Valor).HasPrecision(18, 2);

            modelBuilder.Entity<PreviewActualizacionPrecios>()
                .Property(p => p.FechaCreacion).HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<PreviewActualizacionPrecios>()
                .HasIndex(p => new { p.AdministradorId, p.Aplicado });

            // ---------------- PreviewActualizacionPreciosItem ----------------
            modelBuilder.Entity<PreviewActualizacionPreciosItem>()
                .HasOne(i => i.Preview)
                .WithMany(p => p.Items)
                .HasForeignKey(i => i.PreviewId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PreviewActualizacionPreciosItem>()
                .HasOne(i => i.Producto)
                .WithMany()
                .HasForeignKey(i => i.ProductoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PreviewActualizacionPreciosItem>()
                .Property(i => i.PrecioActual).HasPrecision(18, 2);

            modelBuilder.Entity<PreviewActualizacionPreciosItem>()
                .Property(i => i.PrecioNuevo).HasPrecision(18, 2);

            modelBuilder.Entity<PreviewActualizacionPreciosItem>()
                .HasIndex(i => i.PreviewId);
        }

    }




}
