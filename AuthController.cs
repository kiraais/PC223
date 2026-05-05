using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;

namespace EmersonStudentManagement.Controllers
{
    public class AuthController : Controller
    {
        // GET: Auth/Login
        public ActionResult Login()
        {
            // e clear ang mga session na na save sa storage para dili mag double
            Session.Clear();
            return View();
        }


        // POST: Auth/Login (AJAX Call)
        [HttpPost]
        public JsonResult Login(string username, string password)
        {
            string connStr = ConfigurationManager.ConnectionStrings["EmersonDB"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();
                // JOIN tbl_students so we can grab their First Name!
                string sql = @"SELECT u.*, s.fname 
                               FROM tbl_users u 
                               LEFT JOIN tbl_students s ON u.student_id = s.student_id 
                               WHERE u.username = @user AND u.password = @pass AND u.is_active = 1";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@user", username);
                cmd.Parameters.AddWithValue("@pass", password);

                SqlDataReader reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    Session["userid"] = reader["user_id"].ToString();
                    Session["username"] = reader["username"].ToString();
                    Session["role"] = reader["role"].ToString();

                    // If they are a student, save their First Name. If Admin, just save "Admin"
                    Session["firstname"] = reader["fname"] != DBNull.Value ? reader["fname"].ToString() : "Admin";

                    return Json(new { status = "ok", message = "Login successful.", data = "" }, JsonRequestBehavior.AllowGet);
                }
            }

            return Json(new { status = "error", message = "Invalid username or password.", data = "" }, JsonRequestBehavior.AllowGet);
        }

        // GET: Auth/Logout
        // GET: Auth/ChangePassword
        public ActionResult ChangePassword()
        {
            if (Session["userid"] == null) return RedirectToAction("Login", "Auth");

            ViewBag.Title = "Change Password";
            ViewBag.Subtitle = "Update your account security credentials";

            return View();
        }

        // POST: Update Password (AJAX)
        [HttpPost]
        public JsonResult UpdatePassword(string currentPass, string newPass)
        {
            if (Session["userid"] == null) return Json(new { status = "error", message = "Session expired. Please log in again." });

            try
            {
                string connStr = ConfigurationManager.ConnectionStrings["EmersonDB"].ConnectionString;
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();

                    // 1. Verify the current password
                    string checkSql = "SELECT user_id FROM tbl_users WHERE user_id = @id AND password = @curr";
                    using (SqlCommand cmd = new SqlCommand(checkSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", Session["userid"].ToString());
                        cmd.Parameters.AddWithValue("@curr", currentPass);
                        var result = cmd.ExecuteScalar();

                        if (result == null)
                        {
                            return Json(new { status = "error", message = "Your current password is incorrect." });
                        }
                    }

                    // 2. If correct, update to the new password
                    string updateSql = "UPDATE tbl_users SET password = @new WHERE user_id = @id";
                    using (SqlCommand cmd = new SqlCommand(updateSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@new", newPass);
                        cmd.Parameters.AddWithValue("@id", Session["userid"].ToString());
                        cmd.ExecuteNonQuery();
                    }
                }
                return Json(new { status = "ok", message = "Password updated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { status = "error", message = ex.Message });
            }
        }
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login", "Auth");
        }
    }
}