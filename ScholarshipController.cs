using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;

namespace EmersonStudentManagement.Controllers
{
    public class ScholarshipController : Controller
    {
        string connStr = ConfigurationManager.ConnectionStrings["EmersonDB"].ConnectionString;

        // GET: Scholarship
        public ActionResult Index()
        {
            if (Session["userid"] == null) return RedirectToAction("Login", "Auth");
            if (Session["role"].ToString() != "Admin") return RedirectToAction("Dashboard", "Home");

            ViewBag.Title = "Scholarship Management";
            ViewBag.Subtitle = "Manage scholarship programs and discounts";

            var scholarships = new List<Dictionary<string, string>>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();
                string sql = "SELECT * FROM tbl_scholarships WHERE is_active = 1 ORDER BY scholarship_name";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var s = new Dictionary<string, string>();
                        s["id"] = reader["scholarship_id"].ToString();
                        s["name"] = reader["scholarship_name"].ToString();
                        // Format to remove trailing zeros (e.g., 50.00 -> 50)
                        s["percent"] = Convert.ToDecimal(reader["discount_percent"]).ToString("G29");
                        s["description"] = reader["description"].ToString();
                        scholarships.Add(s);
                    }
                }
            }

            ViewBag.Scholarships = scholarships;
            return View();
        }

        // POST: Save Scholarship (AJAX)
        [HttpPost]
        public JsonResult Save(string name, decimal percent, string description)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    string sql = "INSERT INTO tbl_scholarships (scholarship_name, discount_percent, description) VALUES (@name, @pct, @desc)";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@pct", percent);
                        cmd.Parameters.AddWithValue("@desc", description);
                        cmd.ExecuteNonQuery();
                    }
                }
                return Json(new { status = "ok", message = "Scholarship added successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { status = "error", message = ex.Message });
            }
        }
    }
}