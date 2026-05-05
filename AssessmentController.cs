using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;

namespace EmersonStudentManagement.Controllers
{
    public class AssessmentController : Controller
    {
        string connStr = ConfigurationManager.ConnectionStrings["EmersonDB"].ConnectionString;

        // GET: Assessment
        // GET: Assessment
        public ActionResult Index()
        {
            if (Session["userid"] == null) return RedirectToAction("Login", "Auth");
            if (Session["role"].ToString() != "Admin") return RedirectToAction("Dashboard", "Home");

            ViewBag.Title = "Fee Assessment";
            ViewBag.Subtitle = "Calculate and manage student fee assessments";

            var assessments = new List<Dictionary<string, string>>();
            var students = new List<Dictionary<string, string>>();
            var discounts = new List<Dictionary<string, string>>();

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                // 1. Fetch Assessments AND Calculate Total Paid & Remaining Balance
                string sqlAssess = @"
                    SELECT a.*, s.student_number, s.fname, s.lname, c.course_code, s.year_level,
                           ISNULL((SELECT SUM(amount_paid) FROM tbl_payments WHERE assessment_id = a.assessment_id), 0) AS total_paid
                    FROM tbl_assessments a
                    JOIN tbl_students s ON a.student_id = s.student_id
                    JOIN tbl_courses c ON s.course_id = c.course_id
                    ORDER BY a.assessed_date DESC";

                using (SqlCommand cmd = new SqlCommand(sqlAssess, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var a = new Dictionary<string, string>();
                        a["id"] = reader["assessment_id"].ToString();
                        a["student_name"] = reader["fname"].ToString() + " " + reader["lname"].ToString();
                        a["student_info"] = reader["student_number"].ToString() + " · " + reader["course_code"].ToString() + " Year " + reader["year_level"].ToString();

                        decimal total = Convert.ToDecimal(reader["total_assessment"]);
                        decimal paid = Convert.ToDecimal(reader["total_paid"]);
                        decimal balance = total - paid;

                        a["total"] = total.ToString("N2");
                        a["balance"] = balance.ToString("N2");

                        // 2. Logic for the P, M, S, F Status Dots
                        decimal prelim = Convert.ToDecimal(reader["prelim_amount"]);
                        decimal midterm = Convert.ToDecimal(reader["midterm_amount"]);
                        decimal semi = Convert.ToDecimal(reader["semi_final_amount"]);
                        decimal final = Convert.ToDecimal(reader["final_amount"]);

                        a["pStatus"] = paid >= prelim ? "term-paid" : "term-due";
                        a["mStatus"] = paid >= midterm ? "term-paid" : (paid >= prelim ? "term-due" : "term-pending");
                        a["sStatus"] = paid >= semi ? "term-paid" : (paid >= midterm ? "term-due" : "term-pending");
                        a["fStatus"] = paid >= final ? "term-paid" : (paid >= semi ? "term-due" : "term-pending");

                        // If fully paid, make everything green and final balance green
                        if (balance <= 0)
                        {
                            a["balColor"] = "var(--success)";
                            a["pStatus"] = "term-paid"; a["mStatus"] = "term-paid"; a["sStatus"] = "term-paid"; a["fStatus"] = "term-paid";
                        }
                        else
                        {
                            a["balColor"] = "var(--warn)";
                        }

                        assessments.Add(a);
                    }
                }

                // Fetch Students for Dropdown
                using (SqlCommand cmd = new SqlCommand("SELECT student_id, student_number, fname, lname FROM tbl_students WHERE status = 'Enrolled'", conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var s = new Dictionary<string, string>();
                        s["id"] = reader["student_id"].ToString();
                        s["name"] = reader["student_number"].ToString() + " - " + reader["lname"].ToString() + ", " + reader["fname"].ToString();
                        students.Add(s);
                    }
                }

                // Fetch System Discounts
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM tbl_discounts WHERE is_active=1", conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var d = new Dictionary<string, string>();
                        d["id"] = reader["discount_id"].ToString();
                        d["name"] = reader["discount_name"].ToString() + " (" + Convert.ToDecimal(reader["discount_percent"]).ToString("G29") + "%)";
                        d["percent"] = reader["discount_percent"].ToString();
                        discounts.Add(d);
                    }
                }
            }

            ViewBag.Assessments = assessments;
            ViewBag.Students = students;
            ViewBag.Discounts = discounts;

            return View();
        }

        // POST: Get Student Fee Details (AJAX Auto-fill)
        [HttpPost]
        public JsonResult GetStudentData(int studentId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    // gets the student's units, scholarship %, and the fee rates for their course + school year
                    string sql = @"SELECT s.units_enrolled, s.school_year, 
                                          ISNULL(sch.discount_percent, 0) as schol_pct,
                                          ISNULL(f.tuition_per_unit, 0) as tuition_per_unit,
                                          ISNULL(f.registration_fee, 0) as reg_fee,
                                          ISNULL(f.miscellaneous_fee, 0) as misc_fee,
                                          ISNULL(f.laboratory_fee, 0) as lab_fee
                                   FROM tbl_students s
                                   LEFT JOIN tbl_scholarships sch ON s.scholarship_id = sch.scholarship_id
                                   LEFT JOIN tbl_course_fees f ON s.course_id = f.course_id AND s.school_year = f.school_year
                                   WHERE s.student_id = @sid";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@sid", studentId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return Json(new
                                {
                                    status = "ok",
                                    units = reader["units_enrolled"],
                                    sy = reader["school_year"],
                                    schol_pct = reader["schol_pct"],
                                    tuition_per_unit = reader["tuition_per_unit"],
                                    reg_fee = reader["reg_fee"],
                                    misc_fee = reader["misc_fee"],
                                    lab_fee = reader["lab_fee"]
                                });
                            }
                        }
                    }
                }
                return Json(new { status = "error", message = "Could not find fee rates for this student's course and school year. Please check Course Fees setup." });
            }
            catch (Exception ex)
            {
                return Json(new { status = "error", message = ex.Message });
            }
        }

        // POST: Save Assessment
        [HttpPost]
        public JsonResult Save(int studentId, string schoolYear, decimal totalTuition, decimal regFee, decimal miscFee, decimal labFee, decimal scholDiscount, decimal otherDiscount, decimal netTotal, decimal prelim, decimal midterm, decimal semi, decimal final)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();

                    // THIS was the line that got overwritten! We need to insert into tbl_assessments.
                    string sql = @"INSERT INTO tbl_assessments 
                                  (student_id, school_year, total_tuition, registration_fee, miscellaneous_fee, laboratory_fee, 
                                   scholarship_discount, other_discount, total_assessment, prelim_amount, midterm_amount, semi_final_amount, final_amount) 
                                  VALUES (@sid, @sy, @tt, @reg, @misc, @lab, @sch, @oth, @net, @p, @m, @s, @f)";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@sid", studentId);
                        cmd.Parameters.AddWithValue("@sy", schoolYear);
                        cmd.Parameters.AddWithValue("@tt", totalTuition);
                        cmd.Parameters.AddWithValue("@reg", regFee);
                        cmd.Parameters.AddWithValue("@misc", miscFee);
                        cmd.Parameters.AddWithValue("@lab", labFee);
                        cmd.Parameters.AddWithValue("@sch", scholDiscount);
                        cmd.Parameters.AddWithValue("@oth", otherDiscount);
                        cmd.Parameters.AddWithValue("@net", netTotal);
                        cmd.Parameters.AddWithValue("@p", prelim);
                        cmd.Parameters.AddWithValue("@m", midterm);
                        cmd.Parameters.AddWithValue("@s", semi);
                        cmd.Parameters.AddWithValue("@f", final);
                        cmd.ExecuteNonQuery();
                    }
                }
                return Json(new { status = "ok", message = "Assessment saved successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { status = "error", message = ex.Message });
            }
        }
        // POST: Post Payment (AJAX)
        [HttpPost]
        public JsonResult PostPayment(int assessmentId, string term, decimal amount)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();

                    // ==========================================
                    // 1. VALIDATION: Check Remaining Balance
                    // ==========================================
                    decimal totalAssessed = 0;
                    decimal totalPaidSoFar = 0;
                    int realStudentId = 0;

                    string sqlCheck = @"
                        SELECT 
                            a.student_id,
                            a.total_assessment, 
                            ISNULL((SELECT SUM(amount_paid) FROM tbl_payments WHERE assessment_id = a.assessment_id), 0) AS total_paid
                        FROM tbl_assessments a 
                        WHERE a.assessment_id = @aid";

                    using (SqlCommand cmdCheck = new SqlCommand(sqlCheck, conn))
                    {
                        cmdCheck.Parameters.AddWithValue("@aid", assessmentId);
                        using (SqlDataReader reader = cmdCheck.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                realStudentId = Convert.ToInt32(reader["student_id"]);
                                totalAssessed = Convert.ToDecimal(reader["total_assessment"]);
                                totalPaidSoFar = Convert.ToDecimal(reader["total_paid"]);
                            }
                            else
                            {
                                return Json(new { status = "error", message = "Assessment record not found." });
                            }
                        }
                    }

                    // Calculate what is left to pay
                    decimal remainingBalance = totalAssessed - totalPaidSoFar;

                    // If they owe nothing, stop them.
                    if (remainingBalance <= 0)
                    {
                        return Json(new { status = "error", message = "This assessment is already fully paid." });
                    }

                    // If they try to pay more than they owe, stop them.
                    if (amount > remainingBalance)
                    {
                        return Json(new
                        {
                            status = "error",
                            message = $"Payment exceeds the remaining balance. The student only owes ₱{remainingBalance.ToString("N2")}."
                        });
                    }

                    // ==========================================
                    // 2. INSERT PAYMENT (If validation passes)
                    // ==========================================
                    string sqlInsert = @"INSERT INTO tbl_payments 
                                        (student_id, assessment_id, payment_term, amount_paid, received_by) 
                                        VALUES (@sid, @aid, @term, @amt, @recv)";

                    using (SqlCommand cmd = new SqlCommand(sqlInsert, conn))
                    {
                        cmd.Parameters.AddWithValue("@sid", realStudentId);
                        cmd.Parameters.AddWithValue("@aid", assessmentId);
                        cmd.Parameters.AddWithValue("@term", term);
                        cmd.Parameters.AddWithValue("@amt", amount);

                        string adminName = Session["username"] != null ? Session["username"].ToString() : "Admin";
                        cmd.Parameters.AddWithValue("@recv", adminName);

                        cmd.ExecuteNonQuery();
                    }
                }
                return Json(new { status = "ok", message = "Payment posted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { status = "error", message = ex.Message });
            }
        }
    }
}