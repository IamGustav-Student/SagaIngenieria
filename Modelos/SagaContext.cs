using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace SagaIngenieria.Modelos
{
    public class SagaContext : DbContext
    {
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Vehiculo> Vehiculos { get; set; }
        public DbSet<Ensayo> Ensayos { get; set; }

        // Configuración "Zero Config": La BD se crea en la carpeta del usuario
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SagaIngenieria", "SagaData.db");

            // Asegurar que la carpeta exista
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath));

            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        // Magia para inicialización automática
        public static void Inicializar()
        {
            using (var db = new SagaContext())
            {
                // Crea la base de datos si no existe al arrancar
                db.Database.EnsureCreated();
            }
        }
    }
}