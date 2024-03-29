﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using UcbBack.Logic.B1;
using UcbBack.Models;
using UcbBack.Models.Auth;
using System.Data.Entity;
using UcbBack.Logic;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using UcbBack.Models.Not_Mapped.ViewMoldes;
using System.Configuration;
using UcbBack.Models.Not_Mapped;
using System.Globalization;

namespace UcbBack.Controllers
{
    public class CostCentersController : ApiController
    {
        private ApplicationDbContext _context;
        private B1Connection B1conn;
        public CostCentersController()
        {
            B1conn = B1Connection.Instance();
            _context = new ApplicationDbContext();
        }

        [HttpGet]
        [Route("api/CostCenters/OrganizationalUnits")]
        public IHttpActionResult OrganizationalUnits()
        {
            // Obtener la fecha actual
            DateTime currentDate = DateTime.Now;

            // Consulta a la base de datos con ordenación y filtración
            var result = B1conn.getCostCenter(B1Connection.Dimension.OrganizationalUnit, col: "*")
                .Where(entry =>
                {
                    try
                    {
                        // Intentar convertir la cadena ValidTo a un objeto DateTime
                        DateTime validToDate = DateTime.Parse(entry.ValidTo.ToString());

                        // Verificar si la fecha es mayor que la fecha actual
                        return validToDate > currentDate;
                    }
                    catch (FormatException)
                    {
                        // Manejar el caso en el que ValidTo no es una cadena válida para la fecha
                        return false;
                    }
                })
                .OrderBy(entry => entry.PrcName) // Ordenar por la columna PrcName
                .ToList(); // Convertir a lista antes de devolver

            return Ok(result);
        }



        //[HttpGet]
        //[Route("api/CostCenters/PEI")]
        //public IHttpActionResult PEI()
        //{
        //    var y = B1conn.getCostCenter(B1Connection.Dimension.PEI, col: "*").Cast<JObject>();
        //    return Ok(y);
        //}
        [HttpGet]
        [Route("api/CostCenters/PEI")]
        public IHttpActionResult PEI()
        {
            DateTime currentDate = DateTime.Now;

            var result = B1conn.getCostCenter(B1Connection.Dimension.PEI, col: "*")
                //.Where(entry =>
                //{
                //    try
                //    {
                //// Intentar convertir la cadena ValidTo a un objeto DateTime
                //DateTime validToDate = DateTime.Parse(entry.ValidTo.ToString());

                //// Verificar si la fecha es mayor que la fecha actual
                //return validToDate > currentDate;
                //    }
                //    catch (FormatException)
                //    {
                //// Manejar el caso en el que ValidTo no es una cadena válida para la fecha
                //return false;
                //    }
                //})
                .OrderBy(entry => entry.PrcName) // Ordenar por la columna PrcName
                .Select(entry => new
                {
                    entry.PrcCode,
                    PrcName = entry.PrcName,
                    entry.U_GestionCC,
                    entry.ValidFrom,
                    entry.ValidTo,
                    entry.U_AmbitoPEI,
                    entry.U_DirectrizPEI
                    // Agregar otras propiedades según sea necesario
                })
                .ToList(); // Convertir a lista antes de devolver

            return Ok(result);
        }

        [HttpGet]
        [Route("api/CostCenters/PlanDeEstudios")]
        public IHttpActionResult PlanDeEstudios()
        {
            var y = B1conn.getCostCenter(B1Connection.Dimension.PlanAcademico, col: "*").Cast<JObject>();
            //var y = Careers();
            return Ok(y);
        }
        [HttpGet]
        [Route("api/CostCenters/Paralelo")]
        public IHttpActionResult Paralelo()
        {
            var y = B1conn.getCostCenter(B1Connection.Dimension.Paralelo, col: "*")
                .Select(entry => new
                {
                    entry.PrcCode,
                    entry.PrcName,
                    entry.U_PeriodoPARALELO,
                    entry.U_Sigla,
                    entry.U_Materia,
                    entry.U_Paralelo,
                    entry.U_NivelParalelo,
                    entry.U_TipoParalelo,
                    entry.U_Unidad_Organizacional

                    // Agregar otras propiedades según sea necesario
                })

                .ToList();
            return Ok(y);
        }
        [HttpGet]
        [Route("api/CostCenters/Periodo")]
        public IHttpActionResult Periodo()
        {
            var y = B1conn.getCostCenter(B1Connection.Dimension.Periodo, col: "*").Cast<JObject>();
            return Ok(y);
        }
        [HttpGet]
        [Route("api/CostCenters/Proyectos")]
        public IHttpActionResult Proyectos()
        {
            var y = B1conn.getProjects("*")
                            .OrderBy(item => item["PrjCode"].ToString())
                            .Select(entry => new
                            {
                                entry.PrjCode,
                                entry.PrjName,
                                entry.ValidTo,
                                entry.Active,
                                entry.U_ModalidadProy,
                                entry.U_Sucursal,
                                entry.U_Tipo,
                                entry.U_UORGANIZA,
                                entry.U_PEI_PO

                                // Agregar otras propiedades según sea necesario
                            })

                .ToList();
            return Ok(y);
        }

        //[HttpGet]
        //[Route("api/CostCenters/BusinessPartners")]
        //public IHttpActionResult BP()
        //{
        //    ValidateAuth auth = new ValidateAuth();
        //    CustomUser user = auth.getUser(Request);
        //    var y = B1conn.getBusinessPartners("*", user: user)
        //                    .OrderBy(item => item["CardName"].ToString())
        //                    .Cast<JObject>();
        //    return Ok(y);
        //}


        [HttpGet]
        [Route("api/CostCenters/BusinessPartners")]
        public IHttpActionResult BP()
        {
            ValidateAuth auth = new ValidateAuth();
            CustomUser user = auth.getUser(Request);

            // Consulta original para obtener la información de los Business Partners
            var businessPartners = B1conn.getBusinessPartners("*", user: user)
                                        .OrderBy(item => item["CardName"].ToString())
                                        .Cast<JObject>();

            // Consulta adicional utilizando Entity Framework
            var additionalData = _context.Database.SqlQuery<JObject>(
                "SELECT C.\"BPLName\" " +
                "FROM " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".ocrd A " +
                "INNER JOIN " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".crd8 B ON A.\"CardCode\" = B.\"CardCode\" " +
                "INNER JOIN " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".obpl C ON B.\"BPLId\" = C.\"BPLId\"")
                .OrderBy(item => item.Value<string>("BPLName") ?? "");


            // Combina los resultados de ambas consultas
            var combinedResults = businessPartners.Concat(additionalData);
            Console.WriteLine("AGH", additionalData);

            return Ok(combinedResults);
        }





        //----------------------------- Endpoints accesibles a usuarios NO Admin -----------------------------------
        // Los endpoints de arriba se utilizan en la pestaña de dimensiones SAP solamente
        [HttpGet]
        [Route("api/CostCenters/Careers")]
        public IHttpActionResult Careers()
        //Este endpoint permite obtener la Unidad Organizacional de la carrera
        {
            ValidateAuth auth = new ValidateAuth();

            string query = "select a.\"PrcCode\", a.\"PrcName\", a.\"ValidFrom\", a.\"ValidTo\", a.\"U_NUM_INT_CAR\", a.\"U_Nivel\", b.\"U_CODIGO_DEPARTAMENTO\" as \"UO\", b.\"U_CODIGO_SEGMENTO\", br.\"Id\" as \"BranchesId\" "
                + "from " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".oprc a "
                + "inner join " + ConfigurationManager.AppSettings["B1CompanyDB"] + ".\"@T_GEN_CARRERAS\" b "
                + " on a.\"PrcCode\" = b.\"U_CODIGO_CARRERA\" "
                + "inner join " + CustomSchema.Schema + ".\"Branches\" br "
                + " on br.\"Abr\" = b.\"U_CODIGO_SEGMENTO\" "
                + " WHERE a.\"DimCode\" = " + 3;
            var rawresult = _context.Database.SqlQuery<CostCenterCarrera>(query).OrderBy(x => x.PrcCode);

            var user = auth.getUser(Request);

            var filteredList = auth.filerByRegional(rawresult.AsQueryable(), user).ToList().OrderBy(x => x.PrcCode);

            return Ok(filteredList);
        }
    }
}