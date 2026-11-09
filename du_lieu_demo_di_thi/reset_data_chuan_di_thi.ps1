# ==============================================================================
# KỊCH BẢN RESET DỮ LIỆU CHUẨN ĐI THI (1-CLICK EXAM DATASET RESET)
# Hệ thống Quản lý Thư viện Số UTC — ASP.NET Core MVC (.NET 10.0)
# ==============================================================================

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent $PSScriptRoot
Set-Location $rootDir

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "  KHOI TAO LAI BO DU LIEU CHUAN DI THI (EXAM MASTER DATASET V3)       " -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

Write-Host "[1/2] Dang tai tao bo anh sach upload (.png/.jpg) & file test bao mat..." -ForegroundColor Yellow
powershell -ExecutionPolicy Bypass -File "$rootDir\scripts\generate_exam_assets.ps1"

Write-Host "[2/2] Dang dong bo CSDL SQL Server (library_management) & 140 anh SVG..." -ForegroundColor Yellow
dotnet run --project "$rootDir\web_mvc_management" -- --reseed --seed-only

Write-Host "======================================================================" -ForegroundColor Green
Write-Host "  HOAN TAT! CSDL VA HINH ANH DA SAN SANG 100% DE DEMO BAO VE DI THI!  " -ForegroundColor Green
Write-Host "  Chay ung dung: dotnet run --project web_mvc_management              " -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
