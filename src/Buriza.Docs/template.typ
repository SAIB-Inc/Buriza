#let template(
  title: "", // Title of the document.
  authors: none, // List of authors.
  company: "", // Company name.
  emails: none, // List of emails.
  date: datetime.today().display(), // Date of the document.
  main-color: "6010E9", // Main color of the document.
  alpha: 60%, // Alpha value of the main color.
  body,
) = {
  set document(author: company, title: title)

  // Save heading and body font families in variables.
  let body-font = "Libertinus Sans Serif"
  let title-font = "Libertinus Sans Serif"

  // Set colors
  let primary-color = rgb(main-color) // alpha = 100%
  // change alpha of primary color
  let secondary-color = color.mix(color.rgb(100%, 100%, 100%, alpha), primary-color, space: rgb)

  //customize look of figure
  set figure.caption(separator: [ --- ], position: top)

  // Set body font family.
  set text(font: body-font, 12pt)
  show heading: set text(font: title-font, fill: primary-color)

  //heading numbering
  set heading(numbering: (..nums) => {
    let level = nums.pos().len()
    // only level 1 and 2 are numbered
    let pattern = if level == 1 {
      "I."
    } else if level == 2 {
      "i.1"
    } else if level == 3 {
      "i.1.1"
    } else if level == 4 {
      "i.1.1.1"
    }

    if pattern != none {
      numbering(pattern, ..nums)
    }
  })

  // add space for heading
  show heading.where(level: 1): it => it + v(0.5em)

  // Set link style
  show link: it => underline(text(fill: primary-color, it))

  //numbered list colored
  set enum(
    indent: 1em, numbering: n => [#text(fill: primary-color, numbering("1.", n))],
  )

  //unordered list colored
  set list(indent: 1em, marker: n => [#text(fill: primary-color, "•")])

  // display of outline entries
  show outline.entry: it => text(size: 12pt, weight: "regular", it)

  v(2fr)
  
  // SAIB Logo
  align(center, image("images/saib_logo_dark.png", width: 30%))
  v(2em)

  align(center, text(font: title-font, 3em, weight: 700, title))
  v(5em, weak: true)
  if authors != none {
    align(center, text(font: title-font, 1em, weight: 300, authors))
    v(5em, weak: true)
  }

  align(center, text(1.1em, date))

  v(2fr)

  // Author and other information.
  align(left)[
    #if emails != none { emph(emails.join(linebreak())); linebreak(); }
    #if company != "" { strong(company); linebreak(); }
  ]

  pagebreak()

  set page(
    numbering: "1 / 1", 
    number-align: center, 
    )
  // Main body.
  set page(header: [#emph()[#title #h(1fr) #company]])
  set par(justify: true)

  body
}

#let primary-color = rgb("6010E9")

#let stylize(content) = text([#emph()[#content]], fill: primary-color)
