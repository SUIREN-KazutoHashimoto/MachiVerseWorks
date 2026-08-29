import manifest from '../locales/manifest.json' with { type: 'json' };
import jaJP from '../locales/ja-JP.json' with { type: 'json' };

export type LocaleParameters = Readonly<Record<string, string | number | bigint>>;

type LocaleResource = Readonly<Record<string, string>>;

const resources: Readonly<Record<string, LocaleResource>> = {
  'ja-JP': jaJP,
};

export class Localizer {
  public constructor(
    public readonly locale: string,
    private readonly resource: LocaleResource,
  ) {}

  public t(key: string, parameters: LocaleParameters = {}): string {
    const template = this.resource[key] ?? key;
    return template.replace(/\{([A-Za-z0-9_.-]+)\}/g, (_match, parameterName: string) => {
      const value = parameters[parameterName];
      return value === undefined ? `{${parameterName}}` : String(value);
    });
  }
}

export function initializeLocalization(): Localizer {
  const defaultLocale = manifest.defaultLocale;
  if (!manifest.supportedLocales.includes(defaultLocale)) {
    throw new Error(`Default locale ${defaultLocale} is not listed as supported.`);
  }

  const resource = resources[defaultLocale];
  if (resource === undefined) {
    throw new Error(`Locale resource ${defaultLocale} is not bundled.`);
  }

  document.documentElement.lang = defaultLocale;
  return new Localizer(defaultLocale, resource);
}
