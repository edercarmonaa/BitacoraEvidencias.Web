/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Pages/**/*.{cshtml,cshtml.cs}",
    "./Pages/**/*.cs",
    "./ViewComponents/**/*.{cs,cshtml}",
    "./Views/**/*.cshtml",
    "./Components/**/*.cshtml"
  ],
  theme: {
    extend: {
      colors: {
        brand: {
          guinda: "var(--color-brand-guinda)",
          guindaDark: "var(--color-brand-guinda-dark)",
          arena: "var(--color-brand-arena)",
          crema: "var(--color-brand-crema)",
          ink: "var(--color-ink)"
        }
      },
      borderRadius: {
        md: "var(--radius-md)",
        lg: "var(--radius-lg)",
        xl: "var(--radius-xl)"
      },
      spacing: {
        18: "4.5rem",
        22: "5.5rem"
      },
      fontSize: {
        "fluid-xs": ["clamp(0.75rem, 0.72rem + 0.2vw, 0.88rem)", { lineHeight: "1.4" }],
        "fluid-sm": ["clamp(0.88rem, 0.84rem + 0.25vw, 1rem)", { lineHeight: "1.45" }],
        "fluid-base": ["clamp(1rem, 0.96rem + 0.35vw, 1.12rem)", { lineHeight: "1.5" }],
        "fluid-lg": ["clamp(1.12rem, 1.03rem + 0.6vw, 1.38rem)", { lineHeight: "1.35" }],
        "fluid-xl": ["clamp(1.28rem, 1.13rem + 1vw, 1.78rem)", { lineHeight: "1.25" }]
      },
      fontFamily: {
        sans: [
          "\"Segoe UI Variable\"",
          "\"Montserrat\"",
          "\"Gotham\"",
          "\"Myriad Pro\"",
          "\"Segoe UI\"",
          "sans-serif"
        ]
      }
    }
  },
  safelist: [
    "grid",
    "hidden",
    "block",
    "flex",
    "sm:grid-cols-2",
    "xl:grid-cols-3"
  ]
};
