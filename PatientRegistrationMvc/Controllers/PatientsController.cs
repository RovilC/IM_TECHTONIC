using Microsoft.AspNetCore.Mvc;
using RhuHealthRecords.Models;

namespace RhuHealthRecords.Controllers
{
    public class PatientsController : Controller
    {
        // Patient Directory placeholder so asp-action="Index" resolves; replace with the real list page.
        public IActionResult Index() => Content("Patient Directory");

        [HttpGet]
        public IActionResult Register()
        {
            var model = new PatientRegistrationViewModel
            {
                PatientId = GeneratePatientId(),
                CivilStatus = "Single"
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(PatientRegistrationViewModel model)
        {
            if (model.DateOfBirth.HasValue && model.DateOfBirth.Value.Date > DateTime.Today)
                ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth cannot be in the future.");

            if (!ModelState.IsValid)
            {
                // PatientId is readonly and never trusted from the client; regenerate when missing.
                model.PatientId ??= GeneratePatientId();
                return View(model);
            }

            // TODO: persist the patient, then assign the real sequence-based Patient ID.
            TempData["Success"] = "Patient registered.";
            return RedirectToAction(nameof(Index));
        }

        // Stub: replace the random number with a database-backed sequence.
        private static string GeneratePatientId()
            => $"PT-{DateTime.Today.Year}-{Random.Shared.Next(1, 10000):D4}";
    }
}
