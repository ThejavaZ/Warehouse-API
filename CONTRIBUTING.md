# Contributing to Callio Mobile

Thank you for your interest in contributing to Callio Mobile!

This document describes the basic guidelines for contributing to the project.

## 1. Code of Conduct

All contributors are expected to maintain a respectful, collaborative, and constructive environment.

## 2. Before Contributing

Before starting work:

1. Check existing issues to avoid duplicated work.
2. Discuss significant changes before implementing them.
3. Keep contributions focused and easy to review.
4. Follow the project's existing architecture and conventions.

## 3. Development Environment

Callio Mobile is built with:

- Kotlin
- Jetpack Compose
- Material 3
- Android SDK
- Gradle with Kotlin DSL

Use a compatible Android Studio installation and the JDK version required by the project.

## 4. Branching Strategy

Use descriptive branch names:

| Branch prefix | Purpose |
|---|---|
| `feature/` | New functionality |
| `fix/` | Bug fixes |
| `refactor/` | Code improvements |
| `docs/` | Documentation changes |
| `chore/` | Maintenance and configuration |

Examples:

```text
feature/contact-management
fix/contact-import
refactor/contacts-repository
docs/update-readme
chore/update-gradle
```

## 5. Commit Convention

Callio follows Conventional Commits.

Format:

```text
<type>(<scope>): <description>
```

Common types:

| Type | Description |
|---|---|
| `feat` | New functionality |
| `fix` | Bug fixes |
| `refactor` | Code restructuring |
| `docs` | Documentation changes |
| `test` | Tests |
| `chore` | Maintenance |
| `style` | Formatting changes |

Examples:

```text
feat(contacts): add contact creation
fix(telephony): handle missing phone numbers
refactor(database): simplify contact queries
docs(readme): update project overview
chore(android): configure application dependencies
```

Commit messages should be clear, concise, and written in English.

## 6. Architecture Guidelines

Contributions should respect the project's architectural principles:

- Follow feature-first organization.
- Keep presentation, domain, and data responsibilities separated.
- Use ViewModels to manage UI state.
- Prefer StateFlow for observable UI state.
- Keep business logic outside Composable functions.
- Use repositories to abstract data sources.
- Avoid unnecessary abstractions and dependencies.

Follow SOLID and KISS principles without introducing complexity prematurely.

## 7. Kotlin and Compose Guidelines

- Prefer clear and descriptive names.
- Follow Kotlin coding conventions.
- Use immutable UI state whenever practical.
- Keep Composable functions focused.
- Extract reusable UI components when appropriate.
- Handle loading, success, empty, and error states.
- Avoid unnecessary recompositions and expensive UI operations.

## 8. Testing

Contributions should include appropriate tests whenever practical.

Before submitting changes, verify that:

- The application builds successfully.
- Existing tests pass.
- New functionality behaves as expected.
- No unrelated functionality is broken.

## 9. Pull Requests

Pull Requests should:

1. Explain the purpose of the change.
2. Describe the implemented functionality.
3. Reference related issues when applicable.
4. Include screenshots for significant UI changes.
5. Mention any relevant testing performed.

Keep Pull Requests focused on a single feature or improvement whenever possible.

## 10. Licensing

By contributing to Callio Mobile, you agree that your contributions will be licensed under the **Apache License, Version 2.0**.

You retain copyright ownership of your contributions while granting the rights specified by the project's license.

See the [LICENSE](LICENSE) file for the complete license text.

## 11. Questions and Suggestions

For questions, suggestions, or bug reports, use the project's GitHub Issues.

Thank you for helping improve Callio Mobile!
