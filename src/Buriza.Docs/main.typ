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

#pagebreak()

= Introduction

#pagebreak()

= Buriza Front-End  
== Design

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


== Development

=== Buriza Infrastructure

==== Web View

==== MAUI

==== Blazor

=== Front-End Implementation
==== Tailwind

==== MudBlazor

#pagebreak()
= User Interface

#pagebreak()

= Conclusion

#pagebreak()

= Links

#pagebreak()

= References

Material Design. (2025). Material Design 3. Google. #link("https://m3.material.io/")[https://m3.material.io/]
