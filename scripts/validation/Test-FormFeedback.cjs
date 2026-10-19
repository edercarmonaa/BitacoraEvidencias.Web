// Isolated DOM regression suite. No application, database or real network is used.
// Requires happy-dom 21.0.3 under tmp/form-dom-tests/node_modules (Node 22.20+/24).
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../..');
const source = fs.readFileSync(path.join(root, 'Validation/FormRules.cs'), 'utf8');
const curpPattern = source.match(/CurpPattern = @"([^"]+)"/)[1];
const cctPattern = source.match(/CctPattern = @"([^"]+)"/)[1];
const encode = value => String(value).replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;');
const fields = ['Curp', 'NombreCompleto', 'Cct', 'PeriodoInicio', 'PeriodoFin', 'FechaVerificacion'];
const pause = () => new Promise(resolve => setTimeout(resolve, 30));
const formHtml = (original = null) => `<form method="post" data-ui-loading data-case-form="${original ? 'edit' : 'create'}"
  data-case-original="${encode(JSON.stringify(original))}" data-today="2026-10-19" data-office-date="2026-01-01"
  data-curp-pattern="${encode(curpPattern)}" data-cct-pattern="${encode(cctPattern)}">
  ${fields.map(field => `<input name="Input.${field}" data-val="true" value="${encode(original?.[field] ?? '')}">
    <span data-valmsg-for="Input.${field}" data-valmsg-replace="true"></span>`).join('')}
  <button type="submit" name="action" value="save">Guardar</button></form>`;

module.exports = async function run({ Window }) {
  let checks = 0;
  const verify = (condition, message) => { assert.ok(condition, message); checks++; };
  const setup = async html => {
    const window = new Window({ url: 'http://127.0.0.1:5099' });
    window.document.body.innerHTML = `<main id="app-main">${html}</main>`;
    window.fetch = async () => { throw Error('No network in DOM tests'); };
    for (const file of ['lib/jquery/dist/jquery.min.js', 'lib/bootstrap/dist/js/bootstrap.bundle.min.js',
      'lib/jquery-validation/dist/jquery.validate.min.js', 'lib/jquery-validation-unobtrusive/dist/jquery.validate.unobtrusive.min.js',
      'js/site.js', 'js/form-feedback.js']) window.eval(fs.readFileSync(path.join(root, 'wwwroot', file), 'utf8'));
    await pause();
    // Happy DOM has no layout boxes. Match visible form controls explicitly.
    const makeVisible = () => window.jQuery('form').each(function () {
      window.jQuery(this).validate().settings.ignore = '[type=hidden],:disabled';
    });
    makeVisible();
    let posts = 0;
    window.document.addEventListener('submit', event => {
      if (!event.defaultPrevented) posts++;
      event.preventDefault();
    });
    return { window, document: window.document, form: window.document.querySelector('form'),
      posts: () => posts, makeVisible, close: () => window.happyDOM.abort() };
  };
  {
    const test = await setup('<form method="post" data-office-original="{&quot;NumeroOficio&quot;:&quot;OF-OLD&quot;,&quot;Asunto&quot;:&quot;&quot;}"><input name="Input.NumeroOficio" value="OF-OLD" data-val="true" data-val-required="Numero obligatorio"><input name="Input.Asunto" data-val="true" data-val-required="Asunto obligatorio"></form>');
    verify(test.window.jQuery(test.form).valid(), 'Historical office may keep blank subject');
    test.form.elements.namedItem('Input.NumeroOficio').value = '';
    verify(!test.window.jQuery(test.form).valid(), 'An existing valid office number cannot be cleared');
    await test.close();
  }
  {
    const test = await setup(formHtml().replace('<button type="submit"', '<select name="Input.NivelEducativo" data-val="true"><option>Primaria</option><option>Secundaria</option></select><button type="submit"'));
    test.form.usedLevels = ['Primaria'];
    const level = test.form.elements.namedItem('Input.NivelEducativo');
    verify(!test.window.jQuery(test.form).validate().element(level), 'Occupied education level must be rejected');
    level.value = 'Secundaria';
    verify(test.window.jQuery(test.form).validate().element(level), 'Other education level is allowed');
    await test.close();
  }
  {
    const test = await setup(formHtml());
    const { window, form, document } = test;
    const input = name => form.elements.namedItem(`Input.${name}`);
    input('Curp').focus(); input('Curp').blur();
    verify(input('Curp').classList.contains('is-invalid'), 'Empty CURP must validate on first blur');
    verify(!document.querySelector('.toast'), 'Blur must keep errors inline without repeated toast');
    form.requestSubmit();
    verify(test.posts() === 0 && !form.classList.contains('is-submitting'), 'Invalid form must not submit or stay loading');
    verify(document.querySelector('.toast').textContent.includes('errores adicionales'), 'Toast must summarize and bound error list');
    const values = ['AAAA900101HVZBBB00', 'Nombre de prueba', '184-D2', '2020', '2021', '2026-03-01'];
    fields.forEach((field, i) => input(field).value = values[i]);
    for (const cct of ['30PBH0442H', '30PBH0260Z', '184-D2', '327-DV']) {
      input('Cct').value = cct;
      verify(window.jQuery(form).validate().element(input('Cct')), `Must allow CCT ${cct}`);
    }
    input('FechaVerificacion').value = '2026-10-20';
    verify(!window.jQuery(form).validate().element(input('FechaVerificacion')), 'Future verification must fail');
    input('FechaVerificacion').value = '2025-12-31';
    verify(!window.jQuery(form).validate().element(input('FechaVerificacion')), 'Verification before office must fail');
    input('FechaVerificacion').value = '2026-03-01';
    form.requestSubmit();
    verify(test.posts() === 1, 'Valid case sends one submit');
    await test.close();
  }
  {
    const historical = Object.fromEntries(fields.map(field => [field, null]));
    const test = await setup(formHtml(historical));
    const { form, window } = test;
    form.elements.namedItem('Input.NombreCompleto').value = 'Nombre completado';
    verify(window.jQuery(form).valid(), 'Unchanged historical incomplete data is allowed');
    form.elements.namedItem('Input.Curp').value = 'INVALID';
    verify(!window.jQuery(form).valid(), 'Changed historical field must meet new format');
    form.elements.namedItem('Input.Curp').value = '';
    verify(window.jQuery(form).valid(), 'Returning to historical empty value remains allowed');
    await test.close();
  }
  {
    const test = await setup('<form method="post" data-ui-loading data-confirm="Eliminar registro?"><button type="submit">Eliminar</button></form>');
    test.form.requestSubmit(); test.form.requestSubmit(); await pause();
    verify(test.posts() === 0, 'Confirmation must prevent POST before acceptance');
    test.document.querySelector('[data-confirm-cancel]').click(); await pause();
    verify(test.posts() === 0 && !test.form.classList.contains('is-submitting'), 'Cancelling must send nothing and restore form');
    test.form.requestSubmit(); await pause();
    test.document.querySelector('[data-confirm-accept]').click(); await pause();
    verify(test.posts() === 1, 'Confirmation must send exactly one POST');
    await test.close();
  }
  {
    const test = await setup('<form method="post" data-number-check="/Oficios/Create?handler=ValidateNumber"><input name="Input.NumeroOficio" data-val="true"><span data-valmsg-for="Input.NumeroOficio" data-valmsg-replace="true"></span><button type="submit">Guardar</button></form>');
    const { form, window } = test;
    let response = 'Ya existe un oficio con ese numero.';
    window.fetch = async () => ({ ok: true, headers: new Map([['content-type', 'application/json']]), json: async () => response });
    const input = form.elements.namedItem('Input.NumeroOficio');
    input.value = 'of-001';
    input.focus(); input.blur(); await pause();
    verify(input.classList.contains('is-invalid'), 'Database duplicate appears on blur');
    form.requestSubmit(); await pause();
    verify(test.posts() === 0, 'Duplicate sends no POST');
    response = true; input.value = 'OF-NUEVO'; input.dispatchEvent(new window.Event('input', { bubbles: true }));
    form.requestSubmit(); await pause();
    verify(test.posts() === 1, 'Unique number proceeds once after async check');
    await test.close();
  }
  {
    const test = await setup('<form method="post"><input name="Input.File" type="file" data-val="true" data-file-types=".csv,.xlsx" data-file-max="10"><span data-valmsg-for="Input.File" data-valmsg-replace="true"></span></form>');
    const input = test.form.elements.namedItem('Input.File');
    const valid = () => test.window.jQuery(test.form).validate().element(input);
    verify(!valid(), 'File selection is required');
    Object.defineProperty(input, 'files', { configurable: true, value: [{ name: 'data.exe', size: 3 }] });
    verify(!valid(), 'Unsupported extension is rejected');
    Object.defineProperty(input, 'files', { configurable: true, value: [{ name: 'data.csv', size: 11 }] });
    verify(!valid(), 'Oversized file is rejected');
    Object.defineProperty(input, 'files', { configurable: true, value: [{ name: 'data.csv', size: 3 }] });
    verify(valid(), 'Valid file is accepted');
    await test.close();
  }
  {
    const test = await setup(formHtml());
    const main = test.document.createElement('main'); main.id = 'app-main'; main.innerHTML = formHtml();
    test.document.querySelector('#app-main').replaceWith(main);
    test.document.dispatchEvent(new test.window.CustomEvent('htmx:afterSwap', { detail: { target: main } }));
    test.makeVisible();
    const input = main.querySelector('[name="Input.Curp"]');
    input.focus(); input.blur();
    verify(input.classList.contains('is-invalid'), 'Validation is restored after HTMX swap');
    const errors = test.document.createElement('div'); errors.dataset.formErrors = ''; errors.hidden = true;
    const message = test.document.createElement('span'); message.textContent = '<img src=x onerror=alert(1)>';
    errors.append(message); main.append(errors);
    test.document.dispatchEvent(new test.window.CustomEvent('htmx:afterSwap', { detail: { target: main } }));
    verify(!test.document.querySelector('.toast img'), 'Server messages must be text, never HTML');
    await test.close();
  }
  return { checks, result: 'OK: validation, summaries, history, confirmations, duplicate numbers, files, HTMX and escaping' };
};

if (require.main === module) module.exports({ Window: require(path.join(root, 'tmp/form-dom-tests/node_modules/happy-dom')).Window })
  .then(result => console.log(result)).catch(error => { console.error(error); process.exitCode = 1; });
