using Microsoft.Playwright;

namespace CallCalendar.E2E.Tests;

/// Сквозной сценарий гостя: выбор типа встречи → выбор дня и слота → форма → подтверждение.
public class BookingFlowTests : IClassFixture<E2EApp>
{
    private readonly E2EApp _app;

    public BookingFlowTests(E2EApp app) => _app = app;

    [Fact]
    public async Task Guest_books_a_slot_from_type_selection_to_confirmation()
    {
        if (!_app.Enabled)
        {
            // RUN_E2E=1 не выставлен: обычный `dotnet test` без браузера остаётся зелёным.
            return;
        }

        var page = await _app.NewPageAsync();

        // 1. Выбор типа события (сид-демо «30-минутный созвон»). Клик повторяем, пока
        // Blazor-цепь (SignalR) не поднимется и календарь не отрисуется: первый клик
        // по пререндеренной кнопке может уйти «в пустоту».
        var typeButton = page.Locator("button.choice")
            .Filter(new LocatorFilterOptions { HasText = "30-минутный созвон" });
        var calendarDays = page.Locator("button.day");
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                await typeButton.ClickAsync(new() { Timeout = 2000 });
            }
            catch (Exception)
            {
                // кнопка ещё не готова — повторим на следующей итерации
            }

            if (await calendarDays.CountAsync() > 0)
            {
                break; // календарь отрисовался — тип выбран
            }

            if (attempt == 19)
            {
                throw new TimeoutException("Календарь не появился после выбора типа события.");
            }

            await page.WaitForTimeoutAsync(500);
        }

        // 2. Выбор завтрашнего дня (MSK) — будущий день с полным набором слотов.
        var msk = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
        var nowMsk = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, msk);
        var tomorrow = nowMsk.Date.AddDays(1);
        if (tomorrow.Month != nowMsk.Month)
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Следующий месяц" }).ClickAsync();
        }

        await page.GetByRole(AriaRole.Button, new() { Name = tomorrow.Day.ToString(), Exact = true }).ClickAsync();

        // 3. Выбор первого свободного слота.
        await page.Locator("button.slot").First.WaitForAsync();
        await page.Locator("button.slot").First.ClickAsync();

        // 4. Заполнение формы гостя.
        await page.GetByPlaceholder("Как к вам обращаться").FillAsync("Иван Петров");
        await page.GetByPlaceholder("you@example.com").FillAsync("ivan@example.test");
        await page.GetByRole(AriaRole.Button, new() { Name = "Записаться", Exact = true }).ClickAsync();

        // 5. Подтверждение записи.
        await Assertions.Expect(page.GetByText("Запись подтверждена")).ToBeVisibleAsync();
    }
}
