using Microsoft.EntityFrameworkCore;
using System.IO; // Para manejar rutas de archivos

namespace SagaIngenieria.Modelos
{
    // Esta clase es el puente entre tu código y la base de datos SQLite
    public class SagaContext : DbContext
    {
        // Estas son las "Tablas" que tendrá tu base de datos
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Vehiculo> Vehiculos { get; set; }
        public DbSet<Ensayo> Ensayos { get; set; }

        // Aquí configuramos dónde se guarda el archivo .db
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Guardaremos el archivo "SagaData.db" en la misma carpeta donde corre el programa
            string rutaBaseDatos = "Data Source=SagaData.db";
            optionsBuilder.UseSqlite(rutaBaseDatos);
        }
    }
}
