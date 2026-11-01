// Uses the installed Playwright module and E2E_* environment settings; never saves credentials.
// Run: node src/Exsensic.Web/tests/private-pages.browser.cjs
const { chromium } = require(process.env.P1_PLAYWRIGHT_MODULE || 'playwright');
const fs = require('node:fs');
const base = process.env.E2E_BASE_URL;
const output = 'docs/task3/person1/evidence/private-browser-results.json';
(async () => {
    if (!base) throw new Error('E2E_BASE_URL required');
    const browser = await chromium.launch({ channel: 'chrome', headless: true });
    const results = [], interactions = [];
    async function inspect(page, role) {
        await page.waitForLoadState("domcontentloaded");
        await page.locator("main h1").waitFor();
        for (const width of [360, 390, 768, 1024, 1280]) {
            await page.setViewportSize({ width, height: 900 });
            results.push({ role, route: new URL(page.url()).pathname, width, ...await page.evaluate(() => {
                const ids = [...document.querySelectorAll('[id]')].map(el => el.id);
                return { overflow: document.documentElement.scrollWidth > innerWidth, h1: document.querySelectorAll('main h1').length,
                    duplicateIds: ids.filter((id, i) => ids.indexOf(id) !== i),
                    brokenAria: [...document.querySelectorAll('[aria-describedby],[aria-labelledby],[aria-controls]')].flatMap(el => ['aria-describedby','aria-labelledby','aria-controls'].flatMap(attr => (el.getAttribute(attr) || '').split(/\s+/).filter(id => id && !document.getElementById(id)))),
                    apiFailure: !!document.querySelector('.feedback-error'),
                    unlabelled: [...document.querySelectorAll('main input:not([type=hidden]),main select,main textarea')].filter(el => !el.labels?.length && !el.getAttribute('aria-label') && !el.getAttribute('aria-labelledby')).map(el => el.name) };
            }) });
        }
    }
    try {
        for (const role of ['CLIENT', 'STAFF', 'ADMIN']) {
            const context = await browser.newContext();
            const page = await context.newPage();
            await page.goto(base + '/Account/Login');
            await page.locator('#Email').fill(process.env['E2E_' + role + '_EMAIL']);
            await page.locator('#Password').fill(process.env['E2E_' + role + '_PASSWORD']);
            await page.getByRole('button', { name: 'Sign in', exact: true }).click();
            await page.waitForURL(url => !url.pathname.includes('/Account/Login'));
            interactions.push({ role, action: 'real sign-in', result: 'PASS' });
            if (role === 'CLIENT') {
                await page.goto(base + '/Account/Profile'); await inspect(page, role);
                await page.goto(base + '/Services'); await inspect(page, role);
                const detail = await page.locator('article.service-card a').first().getAttribute('href');
                await page.goto(base + detail); await inspect(page, role);
                await page.getByRole('link', { name: 'Book this service', exact: true }).click(); await inspect(page, role);
                await page.locator('.slot-panel:not([hidden]) input[type=radio]:not(:disabled)').first().check();
                await page.getByRole('button', { name: 'Continue to requirements' }).click(); await inspect(page, role);
                const brief = page.locator('form').filter({ has: page.getByRole('button', { name: 'Submit booking request' }) });
                for (const select of await brief.locator('select').all()) await select.selectOption({ index: 1 });
                for (const field of await brief.locator('textarea').all()) await field.fill('Task 3 local browser acceptance.');
                for (const field of await brief.locator('input:not([type=hidden])').all()) {
                    const type = await field.getAttribute('type');
                    if (type === 'checkbox') await field.check();
                    else await field.fill(type === 'number' ? (await field.getAttribute('min') || '1') : type === 'url' ? 'https://example.com' : 'Task 3 local acceptance');
                }
                await page.getByRole('button', { name: 'Submit booking request' }).click();
                await page.waitForURL(/\/Confirmation$/); await inspect(page, role);
                const createdBooking = new URL(page.url()).pathname.replace('/Confirmation', '');
                interactions.push({ role, action: 'real booking creation', result: 'PASS' });
                await page.goto(base + '/Bookings'); await inspect(page, role);
                const filter = page.getByRole('navigation', { name: 'Booking filters', exact: true });
                await filter.getByRole('link', { name: 'Requested', exact: true }).focus();
                await page.keyboard.press('Enter'); await page.waitForURL(/filter=Requested/);
                interactions.push({ role, action: 'keyboard Requested filter', result: await page.locator('[aria-current=page]').filter({ hasText: 'Requested' }).count() ? 'PASS' : 'FAIL' });
                const booking = createdBooking;
                await page.goto(base + booking); await inspect(page, role);
                for (const label of ['Reschedule booking', 'Review cancellation']) {
                    await page.goto(base + booking);
                    const link = page.getByRole('link', { name: label, exact: true });
                    if (await link.count()) {
                        await link.click(); await inspect(page, role);
                        if (label === 'Reschedule booking') {
                            await page.locator('.slot-panel:not([hidden]) input[type=radio]:not(:disabled)').last().check();
                            await page.getByRole('button', { name: 'Request new time' }).click();
                            await page.waitForURL(base + booking);
                            interactions.push({ role, action: 'real reschedule and refreshed detail', result: await page.locator('main .status-badge').first().textContent().then(text => text.trim() === 'Requested') ? 'PASS' : 'FAIL' });
                        } else {
                            await page.locator('#Reason').fill('Task 3 local acceptance cleanup.');
                            await page.getByRole('button', { name: 'Cancel this booking', exact: true }).click();
                            await page.getByRole('dialog').waitFor();
                            await page.keyboard.press('Escape');
                            interactions.push({ role, action: 'cancel dialog Escape focus return', result: await page.getByRole('button', { name: 'Cancel this booking', exact: true }).evaluate(el => el === document.activeElement) ? 'PASS' : 'FAIL' });
                            await page.getByRole('button', { name: 'Cancel this booking', exact: true }).click();
                            await page.getByRole('dialog').getByRole('button', { name: 'Yes, cancel booking', exact: true }).click();
                            await page.waitForURL(base + booking);
                            interactions.push({ role, action: 'real cancellation and refreshed detail', result: await page.locator('main .status-badge').first().textContent().then(text => text.trim() === 'Cancelled') ? 'PASS' : 'FAIL' });
                        }
                    }
                }
            } else if (role === 'STAFF') {
                await page.goto(base + '/Staff'); await inspect(page, role);
                await page.getByRole('link', { name: 'Next week', exact: true }).focus(); await page.keyboard.press('Enter');
                await page.waitForURL(/week=/); interactions.push({ role, action: 'keyboard next week', result: 'PASS' });
                await page.goto(base + '/Staff');
                const booking = await page.locator('article.booking-card a').first().getAttribute('href');
                await page.goto(base + booking); await inspect(page, role);
            } else {
                await page.goto(base + '/Admin'); await inspect(page, role);
                await page.goto(base + '/Admin/Bookings'); await inspect(page, role);
                await page.locator('#Status').selectOption('Requested');
                await page.getByRole('button', { name: 'Apply filters', exact: true }).focus(); await page.keyboard.press('Enter');
                await page.waitForURL(/status=Requested/i);
                interactions.push({ role, action: 'real POST filter retains Requested', result: await page.locator('#Status').inputValue() === 'Requested' ? 'PASS' : 'FAIL' });
                await inspect(page, role);
                if (!await page.locator('.desktop-bookings tbody a').count()) await page.goto(base + '/Admin/Bookings');
                const booking = await page.locator('.desktop-bookings tbody a').first().getAttribute('href');
                await page.goto(base + booking); await inspect(page, role);
            }
            await context.close();
            // Existing API policy permits 5 logins/minute. Stay below it.
            if (role !== 'ADMIN') await new Promise(resolve => setTimeout(resolve, 13000));
        }
    } finally {
        fs.writeFileSync(output, JSON.stringify({ date: '2026-10-28', results, interactions }, null, 2));
        await browser.close();
    }
    if (results.some(row => row.overflow || row.h1 !== 1 || row.duplicateIds.length || row.brokenAria.length || row.unlabelled.length || row.apiFailure) || interactions.some(row => row.result !== 'PASS')) process.exitCode = 1;
    console.log(JSON.stringify({ inspections: results.length, interactions }));
})().catch(error => { console.error(error.name + ': ' + error.message.split('\n')[0]); process.exitCode = 1; });
