# App Management

Hệ thống quản lý ứng dụng — xây dựng với ASP.NET Core MVC (.NET 11.0).

## 📋 Yêu cầu

- [.NET 11.0 SDK](https://dotnet.microsoft.com/download)
- SQL Server (hoặc SQL Server Express / Docker)

## 🚀 Bắt đầu

### 1. Clone repo

```bash
git clone <repo-url>
cd app_management
```

### 2. Cấu hình môi trường

```bash
cp .env.example .env
# Chỉnh sửa .env với thông tin database thực tế
```

### 3. Cập nhật Connection String

Chỉnh `appsettings.json` hoặc dùng biến môi trường từ `.env`.

### 4. Chạy Migration

```bash
cd web_mvc_management
dotnet ef database update
```

### 5. Chạy ứng dụng

```bash
dotnet run --project web_mvc_management
```

Truy cập: https://localhost:5001

## 📁 Cấu trúc dự án

```
app_management/
├── app_management.sln
├── .gitignore
├── .env.example
├── README.md
└── web_mvc_management/
    ├── Controllers/          # Xử lý request
    ├── Models/
    │   ├── Entities/         # Entity classes (map DB)
    │   └── ViewModels/       # ViewModel cho Views
    ├── Views/                # Razor views
    ├── Services/             # Business logic
    │   └── Interfaces/
    ├── Repositories/         # Data access layer
    │   └── Interfaces/
    ├── Data/                 # DbContext, Migrations, Seed
    ├── Middlewares/          # Custom middlewares
    ├── Helpers/              # Utility classes
    ├── Filters/              # Action/Exception filters
    ├── Extensions/           # Extension methods
    ├── Configurations/       # Strongly-typed config
    └── wwwroot/              # Static files (CSS, JS, images)
```

## 🛠️ Tech Stack

- **Framework:** ASP.NET Core MVC (.NET 11.0)
- **ORM:** Entity Framework Core
- **Database:** SQL Server
- **Frontend:** Bootstrap 5, jQuery

## 📝 License

MIT

