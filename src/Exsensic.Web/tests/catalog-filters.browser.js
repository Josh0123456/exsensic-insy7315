// Actual read-only catalogue requests, keyboard navigation, Back and refresh.
async page => {
    await page.goto('https://localhost:7005/Services');
    await page.getByRole('navigation', { name: 'Service categories' }).getByRole('link', { name: 'Instagram', exact: true }).focus();
    await page.keyboard.press('Enter');
    await page.waitForURL(/category=Instagram/);
    await page.locator('article.service-card').first().waitFor();
    const categories = await page.locator('article.service-card > .eyebrow').allTextContents();
    const selected = await page.locator('main [aria-current=page]').textContent();
    await page.goBack();
    await page.locator('main h1').waitFor();
    const backUnlocked = await page.locator('[data-filter-navigation]').evaluate(el => !el.dataset.navigating && !el.hasAttribute('aria-busy') && !el.querySelector('[aria-disabled]'));
    await page.reload();
    await page.locator('main h1').waitFor();
    const refreshUnlocked = await page.locator('[data-filter-navigation]').evaluate(el => !el.dataset.navigating && !el.hasAttribute('aria-busy'));
    const result = { keyboardCategory: selected === 'Instagram', realResultsMatch: categories.length > 0 && categories.every(text => text.trim() === 'Instagram'), backUnlocked, refreshUnlocked };
    if (Object.values(result).some(value => !value)) throw new Error(JSON.stringify(result));
    return result;
}
