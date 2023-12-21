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
            var y = B1conn.getCostCenter(B1Connection.Dimension.OrganizationalUnit, col: "*").Cast<JObject>();
            //var y = getOrganizationalUnit();
            return Ok(y);
        }
        [HttpGet]
        [Route("api/CostCenters/PEI")]
        public IHttpActionResult PEI()
        {
            var y = B1conn.getCostCenter(B1Connection.Dimension.PEI, col: "*").Cast<JObject>();
            return Ok(y);
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
            var y = B1conn.getCostCenter(B1Connection.Dimension.Paralelo, col: "*").Cast<JObject>();
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
            var y = B1conn.getProjects("*");
            return Ok(y);
        }
        [HttpGet]
        [Route("api/CostCenters/BusinessPartners")]
        public IHttpActionResult BP()
        {
            ValidateAuth auth = new ValidateAuth();
            CustomUser user = auth.getUser(Request);
            var y = B1conn.getBusinessPartners("*", user: user);
            return Ok(y);
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
