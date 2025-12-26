# Project Problems Tracking

## Build Errors (10 total)

### Status: 🔴 CRITICAL - Build Failing

Last updated: 2025-12-27 00:00 EET

---

## Compilation Errors

### 1. Services/ExportService.cs#L4
**Error:** A using clause must precede all other elements defined in the namespace except extern alias declarations
**Status:** ❌ Not Fixed
**Priority:** HIGH

### 2. ViewModels/DashboardViewModel.cs#L16
**Error:** Member modifier 'private' must precede the member type and name
**Status:** ❌ Not Fixed
**Priority:** HIGH

### 3. Services/ExportService.cs#L3
**Error:** A using clause must precede all other elements defined in the namespace except extern alias declarations
**Status:** ❌ Not Fixed
**Priority:** HIGH

### 4. ViewModels/DashboardViewModel.cs#L12
**Error:** } expected
**Status:** ❌ Not Fixed
**Priority:** HIGH

### 5. Services/EncryptionService.cs#L3
**Error:** A using clause must precede all other elements defined in the namespace except extern alias declarations
**Status:** ❌ Not Fixed
**Priority:** HIGH

### 6. ViewModels/DashboardViewModel.cs#L12
**Error:** { expected
**Status:** ❌ Not Fixed
**Priority:** HIGH

### 7. Services/EncryptionService.cs#L2
**Error:** ; expected
**Status:** ❌ Not Fixed
**Priority:** HIGH

### 8. ViewModels/DashboardViewModel.cs#L11
**Error:** Syntax error, ',' expected
**Status:** ❌ Not Fixed
**Priority:** HIGH

### 9. Services/EncryptionService.cs#L2
**Error:** Syntax error, ',' expected
**Status:** ❌ Not Fixed
**Priority:** HIGH

### 10. Services/ExportService.cs#L2
**Error:** A using clause must precede all other elements defined in the namespace except extern alias declarations
**Status:** ❌ Not Fixed
**Priority:** HIGH

---

## Fix Plan

1. ✅ Create PROBLEMS.md file
2. ⏳ Fix Services/EncryptionService.cs (errors #5, #7, #9)
3. ⏳ Fix Services/ExportService.cs (errors #1, #3, #10)
4. ⏳ Fix ViewModels/DashboardViewModel.cs (errors #2, #4, #6, #8)
5. ⏳ Test build
6. ⏳ Update this file with results
7. ⏳ Repeat until all errors are fixed

---

## Notes

- All errors are related to namespace and using directive placement
- Need to ensure using directives come before namespace declarations
- DashboardViewModel has syntax errors with brackets and modifiers
