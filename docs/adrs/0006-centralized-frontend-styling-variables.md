---
title: "ADR 0006: Centralized Frontend Styling Variables"
date: 2025-11-20
status: Accepted
tags: [frontend, styling, sass, css, angular, react, vue, svelte, design-system]
---
# ADR 0006: Centralized Frontend Styling Variables

## Context
Modern frontend applications (Angular, React, Vue, Svelte) require consistent visual design across components and pages. Hardcoded colors, fonts, font sizes, and spacing values scattered throughout component styles lead to several problems:

1. **Inconsistency**: Different developers use slightly different color values (`#333` vs `#323232`) for what should be the same semantic color.
2. **Maintenance burden**: Changing a brand color or font requires hunting through dozens or hundreds of files.
3. **Lack of semantic meaning**: Values like `#FF5733` provide no context about intent (primary action, error state, etc.).
4. **Dark mode / theming difficulty**: Switching themes requires replacing all hardcoded values rather than swapping variable definitions.
5. **Design-development gap**: Designers define a design system with named tokens, but developers implement with raw values.

CSS-in-JS solutions and inline styles exacerbate these issues by spreading style definitions across JavaScript/TypeScript files without a central source of truth.

## Decision
We REQUIRE that all color values, font families, font weights, font sizes, line heights, and spacing values used in frontend applications be defined in a **centralized, comprehensive SASS/SCSS variables file** (or equivalent CSS custom properties file for frameworks without SASS support).

### Implementation Requirements

#### 1. Variable Definition File
Create a central `_variables.scss` (or `variables.scss`) file containing ALL design tokens:

```scss
// Brand Colors
$color-primary: #0066cc;
$color-primary-light: #3385db;
$color-primary-dark: #004999;
$color-secondary: #6c757d;
$color-accent: #ff6b35;

// Semantic Colors
$color-success: #28a745;
$color-warning: #ffc107;
$color-error: #dc3545;
$color-info: #17a2b8;

// Neutral Colors
$color-text-primary: #212529;
$color-text-secondary: #6c757d;
$color-text-disabled: #adb5bd;
$color-background: #ffffff;
$color-background-alt: #f8f9fa;
$color-border: #dee2e6;

// Typography
$font-family-base: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
$font-family-heading: 'Poppins', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
$font-family-mono: 'Fira Code', 'Courier New', monospace;

$font-size-xs: 0.75rem;    // 12px
$font-size-sm: 0.875rem;   // 14px
$font-size-base: 1rem;     // 16px
$font-size-lg: 1.125rem;   // 18px
$font-size-xl: 1.25rem;    // 20px
$font-size-2xl: 1.5rem;    // 24px
$font-size-3xl: 1.875rem;  // 30px
$font-size-4xl: 2.25rem;   // 36px

$font-weight-light: 300;
$font-weight-normal: 400;
$font-weight-medium: 500;
$font-weight-semibold: 600;
$font-weight-bold: 700;

$line-height-tight: 1.25;
$line-height-normal: 1.5;
$line-height-relaxed: 1.75;

// Spacing (8px base unit)
$spacing-xs: 0.25rem;   // 4px
$spacing-sm: 0.5rem;    // 8px
$spacing-md: 1rem;      // 16px
$spacing-lg: 1.5rem;    // 24px
$spacing-xl: 2rem;      // 32px
$spacing-2xl: 3rem;     // 48px
$spacing-3xl: 4rem;     // 64px

// Borders
$border-radius-sm: 0.25rem;
$border-radius-md: 0.5rem;
$border-radius-lg: 1rem;
$border-width: 1px;

// Shadows
$shadow-sm: 0 1px 2px rgba(0, 0, 0, 0.05);
$shadow-md: 0 4px 6px rgba(0, 0, 0, 0.1);
$shadow-lg: 0 10px 15px rgba(0, 0, 0, 0.15);

// Transitions
$transition-fast: 150ms ease-in-out;
$transition-base: 250ms ease-in-out;
$transition-slow: 350ms ease-in-out;
```

#### 2. Framework-Specific Integration

**Angular:**
- Import `_variables.scss` in `styles.scss` or via `angular.json` `stylePreprocessorOptions.includePaths`.
- Use variables in component SCSS files: `@use 'variables' as *;` (SASS module syntax) or `@import 'variables';` (legacy).

**React / Vue / Svelte:**
- If using SASS: Import variables in each component style block or configure build tool (Vite, Webpack) to auto-inject.
- Alternative: Convert to CSS custom properties for runtime theming:

```scss
:root {
  --color-primary: #{$color-primary};
  --font-size-base: #{$font-size-base};
  // ... map all SASS variables to CSS custom properties
}
```

Then use: `color: var(--color-primary);` in styles.

#### 3. Strict Usage Rules
- **NEVER** hardcode color hex/rgb values, font names, or size values directly in component styles.
- **ALWAYS** use the centralized variable/token.
- Pull requests introducing hardcoded style values SHALL be rejected in code review.
- Linters should enforce variable usage where possible (stylelint rules).

#### 4. Dark Mode / Theming
For applications requiring multiple themes:
- Define theme-specific variable overrides in separate files (e.g., `_variables-dark.scss`).
- Use CSS custom properties for runtime theme switching:

```scss
// _variables-light.scss
$color-background: #ffffff;
$color-text-primary: #212529;

// _variables-dark.scss
$color-background: #1a1a1a;
$color-text-primary: #f8f9fa;

// Convert to CSS custom properties dynamically
[data-theme='light'] {
  --color-background: #ffffff;
  --color-text-primary: #212529;
}

[data-theme='dark'] {
  --color-background: #1a1a1a;
  --color-text-primary: #f8f9fa;
}
```

## Consequences

### Positive
1. **Visual consistency**: Single source of truth ensures uniform appearance across the application.
2. **Maintainability**: Brand updates (color refresh, font change) require editing one file, not hundreds of components.
3. **Semantic clarity**: Variable names (`$color-primary`, `$font-size-lg`) convey intent better than raw values.
4. **Theming support**: Centralized variables make dark mode and multi-tenancy theming straightforward.
5. **Design-development alignment**: Variables mirror design system tokens, reducing translation errors.
6. **Performance**: SASS preprocessing eliminates runtime overhead compared to CSS-in-JS solutions.
7. **Accessibility**: Easier to ensure color contrast ratios and readable font sizes when values are centralized and auditable.

### Negative
1. **Initial setup cost**: Requires upfront effort to define comprehensive variable set and refactor existing hardcoded styles.
2. **Learning curve**: Developers unfamiliar with SASS variables or CSS custom properties need onboarding.
3. **Build tooling dependency**: SASS preprocessing requires configured build pipeline (though most modern frameworks include this).
4. **Discipline required**: Team must consistently use variables; no technical enforcement for inline styles in JS frameworks.
5. **Specificity conflicts**: CSS custom properties have lower specificity than inline styles; developers may bypass with `!important` if not careful.

### Mitigation Strategies
- **Automated linting**: Configure stylelint to warn/error on hardcoded color/font values.
- **Component library**: Build a shared component library that uses variables internally, reducing per-component styling.
- **Documentation**: Provide clear examples and enforce in code review guidelines.
- **Gradual migration**: For existing projects, refactor incrementally (prioritize high-traffic pages first).

## Example Usage

### Angular Component
```scss
// product-card.component.scss
@use 'variables' as *;

.product-card {
  background-color: $color-background;
  border: $border-width solid $color-border;
  border-radius: $border-radius-md;
  padding: $spacing-lg;
  box-shadow: $shadow-sm;
  transition: box-shadow $transition-base;

  &:hover {
    box-shadow: $shadow-md;
  }

  .title {
    font-family: $font-family-heading;
    font-size: $font-size-xl;
    font-weight: $font-weight-semibold;
    color: $color-text-primary;
    margin-bottom: $spacing-sm;
  }

  .price {
    font-size: $font-size-2xl;
    font-weight: $font-weight-bold;
    color: $color-primary;
  }
}
```

### React Component (CSS Modules with SASS)
```scss
// Button.module.scss
@use 'variables' as *;

.button {
  font-family: $font-family-base;
  font-size: $font-size-base;
  font-weight: $font-weight-medium;
  padding: $spacing-sm $spacing-lg;
  border-radius: $border-radius-sm;
  border: none;
  cursor: pointer;
  transition: background-color $transition-fast;

  &.primary {
    background-color: $color-primary;
    color: #ffffff;

    &:hover {
      background-color: $color-primary-dark;
    }
  }

  &.secondary {
    background-color: $color-secondary;
    color: #ffffff;

    &:hover {
      background-color: darken($color-secondary, 10%);
    }
  }
}
```

### Vue Component (Scoped Styles)
```vue
<style lang="scss" scoped>
@use '@/styles/variables' as *;

.user-profile {
  .avatar {
    width: 64px;
    height: 64px;
    border-radius: 50%;
    border: 2px solid $color-primary;
  }

  .name {
    font-size: $font-size-lg;
    font-weight: $font-weight-semibold;
    color: $color-text-primary;
    margin-top: $spacing-sm;
  }

  .bio {
    font-size: $font-size-sm;
    color: $color-text-secondary;
    line-height: $line-height-relaxed;
    margin-top: $spacing-xs;
  }
}
</style>
```

### Svelte Component
```svelte
<style lang="scss">
@use 'src/styles/variables' as *;

.notification {
  padding: $spacing-md;
  border-radius: $border-radius-md;
  font-size: $font-size-sm;
  margin-bottom: $spacing-sm;

  &.success {
    background-color: lighten($color-success, 45%);
    color: darken($color-success, 20%);
    border-left: 4px solid $color-success;
  }

  &.error {
    background-color: lighten($color-error, 45%);
    color: darken($color-error, 20%);
    border-left: 4px solid $color-error;
  }
}
</style>
```

## Compliance & Review
- Code reviews MUST verify that no hardcoded color/font values are introduced.
- Stylelint configuration SHOULD include rules enforcing variable usage.
- Design system updates MUST be reflected in the central variables file before component implementation.
- Quarterly audit RECOMMENDED to identify and refactor any violations that slipped through review.

## References
- Design Tokens specification: https://design-tokens.github.io/community-group/format/
- SASS documentation: https://sass-lang.com/documentation/variables
- CSS Custom Properties: https://developer.mozilla.org/en-US/docs/Web/CSS/Using_CSS_custom_properties
- Stylelint rules for enforcing variables: https://stylelint.io/user-guide/rules/
- Material Design theming: https://material.io/design/color/the-color-system.html
- Tailwind CSS design tokens: https://tailwindcss.com/docs/customizing-colors (for inspiration on naming conventions)
