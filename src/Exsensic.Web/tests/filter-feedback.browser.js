// Run with: playwright-cli -s=p1-task3 run-code --filename=src/Exsensic.Web/tests/filter-feedback.browser.js
// Isolated DOM regression tests of the real site.js, not simulated API/E2E success.
async page => {
    const results = [];
    const check = (name, value) => { if (!value) throw new Error(name); results.push({ name, result: 'PASS' }); };
    const script = await (await page.request.get('http://localhost:5104/js/site.js')).text();
    await page.goto('about:blank');
    await page.setContent(`<div id="action-status" role="status"></div>
      <nav data-filter-navigation data-busy-label="Loading bookings…"><a href="https://example.test/Bookings?filter=Requested">Requested</a></nav>
      <form data-single-submit data-filter-form data-busy-label="Applying filters…"><label for="Status">Status</label><select id="Status" name="Status"><option>Requested</option><option>Confirmed</option></select><button type="submit">Apply filters</button></form>`);
    // Use a same-origin URL relative to about:blank's opaque origin via a controlled base URL.
    await page.addScriptTag({ content: script });
    await page.evaluate(() => {
        document.querySelector('a').href = 'about:blank#requested';
        window.originalSetTimeout = window.setTimeout;
        window.originalClearTimeout = window.clearTimeout;
        window.testTimers = [];
        window.setTimeout = callback => { window.testTimers.push(callback); return window.testTimers.length; };
        window.clearTimeout = id => { if (id) window.testTimers[id - 1] = null; };
    });
    const evaluate = () => page.evaluate(async () => {
        const form = document.querySelector('form'), button = form.querySelector('button'), link = document.querySelector('a'), nav = link.parentElement;
        const submit = () => { const event = new SubmitEvent('submit', { bubbles: true, cancelable: true, submitter: button }); form.dispatchEvent(event); return event; };
        const first = submit(); await Promise.resolve();
        const state = { formBusy: !first.defaultPrevented && form.getAttribute('aria-busy') === 'true', label: button.textContent === 'Applying filters…', value: new FormData(form).get('Status') === 'Requested', repeat: submit().defaultPrevented };
        window.dispatchEvent(new PageTransitionEvent('pageshow', { persisted: true }));
        state.history = !form.dataset.submitting && button.textContent === 'Apply filters' && !button.hasAttribute('aria-disabled') && !document.getElementById('action-status').textContent;
        form.querySelector('select').value = 'Confirmed'; submit(); await Promise.resolve();
        state.changedValue = new FormData(form).get('Status') === 'Confirmed';
        window.testTimers.filter(Boolean).forEach(callback => callback());
        state.recovery = !form.dataset.submitting && !form.hasAttribute('aria-busy');
        const click = options => { const event = new MouseEvent('click', { bubbles: true, cancelable: true, button: 0, ...options }); link.dispatchEvent(event); return event; };
        click({ ctrlKey: true }); state.modified = !nav.dataset.navigating;
        click(); state.linkBusy = nav.getAttribute('aria-busy') === 'true' && link.getAttribute('aria-disabled') === 'true';
        state.linkRepeat = click().defaultPrevented;
        window.dispatchEvent(new PageTransitionEvent('pageshow', { persisted: true }));
        state.linkHistory = !nav.dataset.navigating && !link.hasAttribute('aria-disabled');
        const stop = event => event.preventDefault(); form.addEventListener('submit', stop); submit(); await Promise.resolve();
        state.validation = !form.dataset.submitting; form.removeEventListener('submit', stop);
        window.setTimeout = window.originalSetTimeout;
        window.clearTimeout = window.originalClearTimeout;
        return state;
    });
    const state = await evaluate();
    for (const [name, value] of Object.entries(state)) check(name, value);
    return { type: 'isolated DOM regression; no backend data or authentication', checks: results };
}
