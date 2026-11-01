// Run via playwright-cli run-code --filename=... from the repository root.
async page => {
    const results = [], consoleErrors = [], failedAssets = [];
    page.on('console', msg => { if (msg.type() === 'error') consoleErrors.push(msg.text()); });
    page.on('response', response => { if (response.status() >= 400 && /\.(css|js|svg|ico)(\?|$)/.test(response.url())) failedAssets.push(response.url()); });
    for (const route of ['/', '/Services', '/Account/Login', '/Account/Register', '/Privacy', '/Account/AccessDenied', '/not-a-real-page', '/Error']) {
        for (const width of [360, 390, 768, 1024, 1280]) {
            await page.setViewportSize({ width, height: 900 });
            const response = await page.goto('https://localhost:7005' + route);
            const inspection = await page.evaluate(() => {
                const elements = [...document.querySelectorAll('[id]')];
                return {
                    overflow: document.documentElement.scrollWidth > innerWidth,
                    h1: document.querySelectorAll('main h1').length,
                    duplicateIds: elements.filter((el, i) => elements.findIndex(other => other.id === el.id) !== i).map(el => el.id),
                    brokenAria: [...document.querySelectorAll('[aria-describedby],[aria-labelledby],[aria-controls]')].flatMap(el => ['aria-describedby','aria-labelledby','aria-controls'].flatMap(attr => (el.getAttribute(attr) || '').split(/\s+/).filter(id => id && !document.getElementById(id)))),
                    unlabelled: [...document.querySelectorAll('main input:not([type=hidden]), main select, main textarea')].filter(el => !el.labels?.length && !el.getAttribute('aria-label') && !el.getAttribute('aria-labelledby')).map(el => el.name),
                    apiError: !!document.querySelector('.feedback-error'),
                    emptyState: !!document.querySelector('.empty-state')
                };
            });
            results.push({ route, width, status: response.status(), ...inspection });
        }
    }
    await page.goto('https://localhost:7005/');
    await page.setViewportSize({ width: 390, height: 900 });
    await page.keyboard.press('Tab');
    const skip = await page.locator(':focus').textContent();
    await page.keyboard.press('Enter');
    const skipTarget = await page.evaluate(() => document.activeElement.id);
    await page.getByRole('button', { name: 'Menu', exact: true }).focus();
    await page.keyboard.press('Enter');
    await page.waitForFunction(() => document.getElementById('primary-navigation').classList.contains('show'));
    await page.getByRole('link', { name: 'Services', exact: true }).focus();
    await page.keyboard.press('Escape');
    const menuReturn = await page.locator(':focus').textContent();
    await page.screenshot({ path: 'docs/task3/person1/evidence/home-390.png', fullPage: true });
    await page.goto('https://localhost:7005/Account/Register');
    await page.setViewportSize({ width: 1280, height: 900 });
    await page.screenshot({ path: 'docs/task3/person1/evidence/register-1280.png', fullPage: true });
    return { date: '2026-10-28', browser: 'installed Chrome via Playwright', results, keyboard: { skip, skipTarget, menuReturn }, failedAssets, applicationConsoleErrors: [...new Set(consoleErrors.filter(text => !text.includes('kaspersky')))], environmentConsoleErrors: [...new Set(consoleErrors.filter(text => text.includes('kaspersky')))].length };
}
