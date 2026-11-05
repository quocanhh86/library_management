using Microsoft.AspNetCore.Mvc;

namespace web_mvc_management.ViewComponents;

/// <summary>
/// Ô tìm kiếm sách có gợi ý khi gõ. Dùng ở header độc giả (compact) và trang chủ (hero).
/// </summary>
public class SearchBarViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(bool hero = false, bool autofocus = false)
    {
        var keyword = Request.Query["keyword"].ToString();
        var scope = Request.Query["scope"].ToString();
        return View((Keyword: keyword, Scope: scope, Hero: hero, Autofocus: autofocus));
    }
}
