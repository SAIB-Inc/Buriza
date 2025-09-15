#import "template.typ": *

#show: template.with(
  title: "Buriza Wallet: UI/UX Design and Front-End Development",
  company: "SAIB Inc.",
  date: datetime.today().display(),
  main-color: "#4F8EFF",
  authors: [Clark Alesna#super[1] #h(20pt) Stan Kiefer Gallego#super[2] #h(20pt) Caitlin Lindsay#super[3]],
  emails: (
    "clark@saib.dev", "stan.gallego@saib.dev", "caitlin.lindsay@saib.dev"
  ),
)

 #outline(
    title: [Table of Contents],
    depth: 3,
    indent: 2em,
)

#pagebreak()

= Introduction

#pagebreak()

= Buriza Front-End 
This section covers the branding and UI/UX design of the Buriza wallet, describing the updates made since the first submission.
Additionally, it tackles the implementation of the design - detailing the project set up and various tools utilized.

== Design
This section outlines Buriza’s design evolution in alignment with the principles and best practices of Google’s Material Design 3.

Buriza's design has significantly evolved since the submission of Project Catalyst's Milestone 1,
with the initial branding and UI/UX designs detailed in the following 
#link("https://saibph-my.sharepoint.com/:b:/g/personal/accounts_saib_dev/Ea17Rbt3BLlFvIbwLlNMhMcB0LgqVCZc94Jx55p-FHo8zw?e=KheTGY")[report].

A key transformation in Buriza's branding is the introduction of its new mascot, Buriza Frostling. 
Inspired by the yeti, Frostling’s energetic expressions and bold, resilient personality stand in contrast 
to the earlier delicate, pastel mascots. This strong character is present throughout the application’s 
design, reinforcing Buriza’s core values of security, trust, and power.

The updated branding report also features enhanced iconography, an expanded color palette, 
and clean mockups that showcase Buriza’s modern and sleek aesthetic. These comprehensive changes can 
be viewed in the revised 
#link("https://www.figma.com/design/YVzczU3XoKagofQP5gOQZI/Buriza?node-id=1151-1432")[branding
report], which includes detailed descriptions and illustrations of the logo and typography.

#v(2em)

#figure(
  image("images/buriza_mockup.png"),
  caption: [Buriza Mockup: featuring the mascot, colors, and logo]
)

These developments are reflected in the overall UI and UX of the wallet, resulting in a more refined and visually cohesive application. The improvements stem from the opportunity the SAIB Inc. design team had to explore Material Design 3, using its guidelines to further elevate the project's look and feel. These refinements can be seen in detail in the following #link("https://www.figma.com/design/9Bw3Nuh8UF9xYHkURwJeoI/Buriza-with-MD3?node-id=379-125141&t=cVzlkeRQmhBVQk0d-1")[Figma
 file].

 #pagebreak();

=== Material Design 3

Material Design is a design system created by Google designers and developers that provides UX foundations,
 guidance, and UI components across Android, Flutter, and the Web. Its most recent update, Material Design 
 3, highlights emotional impact. Placing great significance on expressiveness, dynamism, and accessibility,  
 Material Design 3 expands the system with additional shapes, typography guidelines, intuitive motions, 
 and more (#link("https://m3.material.io/")[Material Design 3], 2025).

SAIB's design team studied the foundations of this update, noting and implementing changes based on Material Design 3's guidelines on color, elevation, icons, shape, motion, and typography. The impact of this study can be seen in the most recent design. Buriza's colors have been evaluated for contrast, and the initial color scheme has been updated to a more vibrant palette with accents and a greater variety of shades established for uniformity. Elevation is utilized to provide visual hierarchy and interaction guides for users, and labels have been added to the primary menu icons. Similar considerations have been applied to the wallet's shapes, with Material Design 3 button group designs added to many main pages and expressive animations applied in the main header. While Buriza's initial typography has not changed, revisions have been made to consider type roles within pages.

#v(2em)

#figure(
  image("images/buriza_home_evolution.png", width: 80%),
  caption: [Buriza Mobile UI - Home: design evolution from first to third iteration (left to right)]
)

#figure(
  image("images/buriza_history_evolution.png", width: 80%),
  caption: [Buriza Mobile UI - History: design evolution from first to third iteration (left to right)]
)

#v(3em)

#figure(
  image("images/buriza_send_evolution.png"),
  caption: [Buriza Desktop UI - Send Assets: design evolution from first to third iteration (left to right)]
)

#v(2em)

Buriza's latest design update embodies SAIB's desire to create an impactful application that continues to evolve and improve. The team strives for continuous design iteration and improvement. With the future release of prototypes or mockups to the public, SAIB will continue to evolve through community feedback, validating users' needs and experiences while augmenting improvements with design solutions backed by research and best practices.

#pagebreak()

== Development

This section covers the technical implementation of Buriza's wallet suite, including architecture decisions, platform implementations, and front-end technologies.

=== Project Architecture

Buriza's architecture is built around component sharing and platform independence, enabling a single codebase to serve multiple deployment targets.

==== Solution Structure

Buriza uses a modular architecture with 5 .NET 9 projects designed for scalability and maintainability:

- *`Buriza.UI`* - Shared Blazor component library containing all user interface elements
- *`Buriza.Data`* - Core data models and services for wallet functionality
- *`Buriza.Extension`* - Browser extension for seamless dApp interaction
- *`Buriza.Web`* - Progressive web app for universal access
- *`Buriza.App`* - Cross-platform MAUI app for mobile and desktop

This architecture eliminates the need to implement separate UI layers for each platform. Instead of building distinct interfaces for the browser extension, web app, and mobile app, all platforms consume the same *`Buriza.UI`* components. This approach ensures consistent user experience while dramatically reducing development effort and maintenance overhead.

==== Shared Component Library (*`Buriza.UI`*)

The component library provides a comprehensive set of reusable Blazor components organized into a hierarchical structure:

- *Common* - Foundational UI primitives like buttons, text fields, and navigation tabs
- *Controls* - Advanced interactive elements including asset cards and search functionality
- *Layout* - Application structure components for sidebars, headers, and main content areas
- *Pages* - Complete page/screen implementations for wallet operations like assets, transaction history, dapp access, send, and receive

The library leverages MudBlazor's Material Design 3 implementation alongside Tailwind CSS for utility-first styling and responsive design patterns. This combination ensures both visual consistency and flexible customization across all platform implementations.

=== Platform Implementations

Each platform implementation leverages the shared *`Buriza.UI`* components while providing platform-specific features and deployment mechanisms.

==== Browser Extension

The browser extension represents Buriza's most integrated approach to decentralized web interaction, embedding wallet functionality directly into the user's browsing experience. Built with Blazor WebAssembly and the Blazor.BrowserExtension framework, the extension follows Manifest V3 standards to ensure compatibility with modern browser security requirements and enhanced performance through service workers.

==== Progressive Web App

The progressive web application serves as Buriza's most accessible deployment target, requiring only a modern web browser to provide full wallet functionality. Built with Blazor WebAssembly, the PWA delivers near-native performance by compiling C\# code to WebAssembly bytecode that executes directly in the browser's runtime environment.

The PWA architecture enables installation directly from the browser without requiring app store distribution, creating a native-like experience while maintaining web-based deployment advantages.

==== Cross-Platform App

The #link("https://learn.microsoft.com/en-us/dotnet/maui/")[MAUI] application targets iOS, Android, macOS, and Windows through a hybrid architecture that combines native platform capabilities with web-based UI components. At the core of this implementation is BlazorWebView, a native control that hosts Blazor content within a webview container while providing seamless integration with platform-specific APIs.

BlazorWebView acts as a bridge between the native MAUI shell and the Blazor UI layer, allowing the same *`Buriza.UI`* components to render within a native application context. This approach eliminates the need to rebuild the entire user interface using platform-specific controls like XAML, while still providing access to native device features such as secure storage, biometric authentication, and push notifications.

The hybrid model significantly reduces development complexity by leveraging existing web technologies and shared component libraries. Rather than maintaining separate native codebases for each platform, the application shares a single UI implementation while the MAUI framework handles platform-specific compilation and native API integration automatically.

=== Front-End Implementation

The front-end implementation combines modern web technologies with component-driven architecture to deliver consistent user experiences across all platform deployments.

==== Blazor Components

#link("https://learn.microsoft.com/en-us/aspnet/core/blazor/")[Blazor] is Microsoft's web framework that serves as the foundational UI technology for Buriza, enabling C\# development for web interfaces through WebAssembly compilation. This Microsoft-developed framework allows the entire Buriza application stack to use a single programming language, eliminating context switching between backend and frontend development.

The component architecture follows a hierarchical structure where complex UI elements are composed of smaller, reusable primitives. Blazor's two-way data binding simplifies form interactions and real-time updates, particularly important for wallet operations that require immediate visual feedback on transaction states and balance changes.

==== MudBlazor Design System

#link("https://mudblazor.com/")[MudBlazor] v8.11.0 provides the Material Design 3 foundation for Buriza's visual language, delivering pre-built components with accessibility standards and smooth animations. The theming system integrates seamlessly with CSS custom properties, allowing Buriza to maintain brand identity while leveraging Material Design's proven usability patterns.

Buriza extends this foundation with Buriza-styled components:

*Common Components:*
- *BurizaButton* - Standardized button with consistent styling and interaction states
- *BurizaTextField* - Custom text input with validation and wallet-specific formatting
- *BurizaTabs* - Navigation tabs with custom styling for asset type switching
- *BurizaSelect* - Dropdown selection with wallet account and asset filtering
- *BurizaHeader* - Consistent header layout across all pages

*Control Components:*
- *BurizaAssetCard* - Asset display with balance, price changes, and interactive actions
- *BurizaSearchBar* - Universal search functionality for assets and transactions
- *BurizaDappCard* - dApp connection interface with authorization controls

==== Tailwind CSS Integration

#link("https://tailwindcss.com/")[Tailwind CSS] v4 complements MudBlazor by providing utility-first styling capabilities for custom layouts and responsive behavior. The integration uses a Bun build pipeline for rapid CSS compilation and automatic purging of unused styles, ensuring optimal bundle sizes.

The utility-first approach enables precise control over spacing, positioning, and responsive breakpoints, particularly valuable for wallet interfaces that require exact alignment for transaction details and balance displays. Custom CSS variables bridge the gap between Tailwind utilities and MudBlazor's theming system.

==== Responsive Design Patterns

Buriza implements a mobile-first responsive design strategy that adapts to various screen sizes and interaction methods. The design system accommodates everything from mobile browser extensions to desktop applications while maintaining usability and visual hierarchy.

The responsive approach extends beyond screen size to consider platform-specific interaction patterns. Touch targets are appropriately sized for mobile interfaces, while desktop versions support keyboard navigation and hover states, ensuring consistent functionality regardless of how users access Buriza.

#pagebreak()
= User Interface

#pagebreak()

= Conclusion

#pagebreak()

= References

Google. (2025). _Material Design 3_. Material Design. https://m3.material.io/

Google. (2023). _Manifest V3 - Chrome Extensions_. Chrome Developers. https://developer.chrome.com/docs/extensions/mv3/

Microsoft. (2024). _ASP.NET Core Blazor_. Microsoft Learn. https://learn.microsoft.com/en-us/aspnet/core/blazor/

Microsoft. (2024). _.NET Multi-platform App UI (.NET MAUI)_. Microsoft Learn. https://learn.microsoft.com/en-us/dotnet/maui/

Microsoft. (2024). _BlazorWebView for .NET MAUI_. Microsoft Learn. https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/blazorwebview

Mozilla. (2023). _Progressive web apps (PWAs)_. MDN Web Docs. https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps

MudBlazor Team. (2024). _MudBlazor - Blazor component library_. MudBlazor. https://mudblazor.com/

Tailwind Labs. (2024). _Tailwind CSS - A utility-first CSS framework_. Tailwind CSS. https://tailwindcss.com/

WebAssembly Community Group. (2023). _WebAssembly_. WebAssembly. https://webassembly.org/

#pagebreak()

= Links

#pagebreak()

= References

Material Design. (2025). _Material Design 3_. Google. #link("https://m3.material.io/")[https://m3.material.io/]
