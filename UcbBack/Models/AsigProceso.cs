using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    // IMPORTANTE: el string del CustomSchema es el nombre de la tabla en HANA
    [CustomSchema("Asig_Proceso")]
    [Table("Asig_Proceso")] // evita el plural raro "AsigProcesoes"
    public class AsigProceso
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public int BranchesId { get; set; }

        // Ojo: esto lo estamos manejando como string (ej. "2S2020")
        public string PeriodoId { get; set; }

        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public int? LastUpdateBy { get; set; }
        public string State { get; set; }

        /// <summary>
        /// Genera el siguiente Id como MAX(Id)+1 directamente en HANA.
        /// NO usa secuencia (porque no la tienes todavía) ni LINQ (para evitar el SQL raro).
        /// </summary>
        public static int GetNextId(ApplicationDbContext _context)
        {
            // CustomSchema.Schema = nombre del esquema (ej. ADMNALRRHH),
            // "Asig_Proceso" = nombre real de la tabla en HANA.
            var sql =
                "SELECT COALESCE(MAX(\"Id\"), 0) + 1 AS \"NextId\" " +
                "FROM \"" + CustomSchema.Schema + "\".\"Asig_Proceso\"";

            // Esto genera algo como:
            // SELECT COALESCE(MAX("Id"), 0) + 1 AS "NextId"
            // FROM "ADMNALRRHH"."Asig_Proceso"
            //
            // que es SQL válido en HANA.
            Console.WriteLine("GetNextId SQL => " + sql);

            return _context.Database.SqlQuery<int>(sql).Single();
        }
    }
}
