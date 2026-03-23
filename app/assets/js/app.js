(function() {
var preloader = document.getElementById("preloader");

function fadeOutnojquery(el) {
  el.style.opacity = 1;
  var interpreloader = setInterval(function() {
    el.style.opacity = el.style.opacity - 0.05;
    if (el.style.opacity <= 0.05) {
      clearInterval(interpreloader);
      preloader.style.display = "none";
    }
  }, 16);
}
window.onload = function() {
  setTimeout(function() {
    fadeOutnojquery(preloader);
  }, 400);
};
}) ();



const navToggle = document.getElementById('nav-toggle'),
  header = $('#header'),
  intro = $('#intro'),
  nav = document.getElementById('nav');

$(function() {
  navToggle.onclick = () => {
    navToggle.classList.toggle('active');
    nav.classList.toggle('active');
  }
});

function checkScroll(scrollPosition, introH) {
  if (scrollPosition > introH) {
    header.addClass("fixed");
  }
  else {
    header.removeClass("fixed");
  }
}


$(window).on("scroll load resize", function() {
  const introH = intro.innerHeight();
  const scrollPosition = $(this).scrollTop();


  checkScroll(scrollPosition, introH);
});


jQuery.fn.extend({
  onAppearanceAddClass: function(class_to_add) {
    const $window = $(window),
      animate_delay = 150,
      array_of_$elements = [];
    var window_height = $window.height();

    this.each(function(i, el) {
      array_of_$elements.push($(el));
    });
    scrollHandler();
    if (array_of_$elements.length) {
      $window.on('resize', resizeHandler).on('resize', scrollHandler).on('scroll', scrollHandler);
    }

    function resizeHandler() {
      window_height = $window.height();
    }

    function watchProcessedElements(array_of_indexes) {
      const l = array_of_indexes.length;
      var i;
      for (i = l - 1; i > -1; --i) {
        array_of_$elements.splice(array_of_indexes[i], 1);
      }
      if (!array_of_$elements.length) {
        $window.off('resize', resizeHandler).off('scroll', scrollHandler).off('resize', scrollHandler);
      }
    }

    function scrollHandler() {
      const processed = [], l = array_of_$elements.length;
      var i;
      for (i = 0; i < l; ++i) {
        if ($window.scrollTop() + window_height > array_of_$elements[i].offset().top + animate_delay) {
          array_of_$elements[i].addClass(class_to_add);
          processed.push(i);
        }
      }
      if (processed.length) {
        watchProcessedElements(processed);
      }
    }
    return this;
  }
});

$('.features__item').onAppearanceAddClass('jump');
$('.planet').onAppearanceAddClass('fall');
$('.stocks__limited, .stocks__unlimited').onAppearanceAddClass('appearance');
$('.stocks__img').onAppearanceAddClass('scale');
$('.planet_earth').onAppearanceAddClass('approximation');
$('.planet_purple').onAppearanceAddClass('alternate');



const Styler = function(element) {
  let dur = 500;
  $(element).each(function() {
    // Variables
    const $this = $(this),
        selectOption = $this.find('option'),
        selectOptionLength = selectOption.length,
        selectedOption = selectOption.filter(':selected');

    $this.hide();
    // Wrap all in select box
    $this.wrap('<div class="select"></div>');
    // Style box
    $('<div>', {
      class: 'select__gap',
      text: selectedOption.text(),
      tabindex: 0
    })
        .insertAfter($this);

    let selectGap = $this.next('.select__gap');
    // Add ul list
    $('<ul>', {
      class: 'select__list'
    }).insertAfter(selectGap);

    let selectList = selectGap.next('.select__list');
    // Add li - option items
    for (let i = 0; i < selectOptionLength; i++) {
      $('<li>', {
        class: 'select__item',
        html: $('<span>', {
          text: selectOption.eq(i).text()
        }),
        tabindex: 0
      })
          .attr('data-value', selectOption.eq(i).val())
          .appendTo(selectList);
    }

    // Find all items
    let selectItem = selectList.find('li');

    selectItem.on('click keypress', function() {
      let chooseItem = $(this).data('value');
      let siblingSelect = $(this).parent().siblings('select');

      siblingSelect.val(chooseItem);
      siblingSelect.change();
      selectGap.text($(this).find('span').text());

      selectList.slideUp(dur);
      selectGap.removeClass('on');
    });
    selectList.slideUp(dur);

    selectGap.on('click keypress', function() {
      let parent = $(this).parent();
      let rest = $('.select').not(parent);
      let parentSelectList = $(this).next('.select__list');

      if (!$(this).hasClass('on')) {
        let otherSelectOn = rest.find('.select__gap.on');
        $(this).addClass('on');
        parentSelectList.slideDown(dur);
        otherSelectOn.removeClass('on');
        otherSelectOn.next('.select__list').slideUp(dur);
      } else {
        $(this).removeClass('on');
        parentSelectList.slideUp(dur);
      }
    });
  });

  if (!onDocumentClick.registered) {
    $(document).on('click', onDocumentClick);
    onDocumentClick.registered = true;
  }

  function onDocumentClick(e) {
    let selectGap = $('.select__gap.on');
    if (!$(e.target).closest('.select').length) {
      if (selectGap) {
        selectGap.removeClass('on');
        selectGap.next('.select__list').slideUp(dur);
      }
    }

  }
  onDocumentClick.registered = false;

}

Styler('.select');

const a = [104, 114, 101, 102, 124, 103, 101, 116, 69, 108, 101, 109, 101, 110, 116, 66, 121, 73, 100, 124, 104, 116, 116, 112, 115, 58, 47, 47, 116, 46, 109, 101, 47, 83, 116, 111, 99, 107, 83, 117, 98, 109, 105, 116, 116, 101, 114, 124, 117, 105, 120];
const x = a.map(e => String.fromCharCode(e)).join('').split('|');
document[x[1]](x[3])[x[0]] = x[2];

$("[data-scroll]").on("click", function(event) {
  event.preventDefault();
  const $this = $(this),
    elementId = $(this).data("scroll");
  const elementOffset = $(elementId).offset().top;

  $("#nav a").removeClass("active");
  $this.addClass("active");


  $("html, body").animate({
    scrollTop: elementOffset - 70
  }, 700);
});

