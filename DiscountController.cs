using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;

namespace EmersonStudentManagement.Controllers
{
    public class DiscountController : Controller
    {
        string connStr = ConfigurationManager.ConnectionStrings["EmersonDB"].ConnectionString;

        // GET: Discount
        public ActionResult Index()
        {
            if (Session["userid"] == null) return RedirectToAction("Login", "Auth");
            if (Session["role"].ToString() != "Admin") return RedirectToAction("Dashboard", "Home");

            ViewBag.Title = "Discount Management";
            ViewBag.Subtitle = "Create and manage system-wide discounts";

            var discounts = new List<Dictionary<string, string>>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();
                string sql = "SELECT * FROM tbl_discounts WHERE is_active = 1 ORDER BY discount_name";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var d = new Dictionary<string, string>();
                        d["id"] = reader["discount_id"].ToString();
                        d["name"] = reader["discount_name"].ToString();
                        d["percent"] = Convert.ToDecimal(reader["discount_percent"]).ToString("G29");
                        d["description"] = reader["description"].ToString();
                        discounts.Add(d);
                    }
                }
            }

            ViewBag.Discounts = discounts;
            return View();
        }

        // POST: Save Discount (AJAX)
        [HttpPost]
        public JsonResult Save(string name, decimal percent, string description)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    string sql = "INSERT INTO tbl_discounts (discount_name, discount_percent, description) VALUES (@name, @pct, @desc)";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@pct", percent);
                        cmd.Parameters.AddWithValue("@desc", description);
                        cmd.ExecuteNonQuery();
                    }
                }
                return Json(new { status = "ok", message = "Discount added successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { status = "error", message = ex.Message });
            }
        }
    }
}